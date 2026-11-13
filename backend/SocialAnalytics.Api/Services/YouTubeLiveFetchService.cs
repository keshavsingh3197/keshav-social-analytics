using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// Fetches live public channel metadata and latest uploaded videos for YouTube channels.
/// Supports YouTube Data API v3 when Admin configures an API key, with automatic keyless
/// fallback using YouTube handle page resolution + official public Atom RSS video feeds.
/// </summary>
public sealed partial class YouTubeLiveFetchService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<YouTubeLiveFetchService> _logger;

    public YouTubeLiveFetchService(IHttpClientFactory httpFactory, ILogger<YouTubeLiveFetchService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<bool> EnrichChannelAsync(ChannelMetrics channel, string? apiKey, CancellationToken ct = default)
    {
        if (channel.Platform != SocialPlatform.YouTube)
            return false;

        var http = _httpFactory.CreateClient("LiveYouTube");
        var updated = false;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            updated = await TryEnrichViaApiV3Async(http, channel, apiKey.Trim(), ct);
        }

        if (!updated)
        {
            updated = await TryEnrichViaPublicFeedAsync(http, channel, ct);
        }

        if (updated)
        {
            channel.IsVerifiedLive = true;
            channel.LastSyncedAt = DateTime.UtcNow;
            if (channel.TotalViewsLast30Days > 0 && channel.RpmUsd > 0)
            {
                channel.EstimatedMonthlyRevenue = Math.Round((channel.TotalViewsLast30Days / 1000.0) * channel.RpmUsd, 2);
            }
        }

        return updated;
    }

    private async Task<bool> TryEnrichViaApiV3Async(HttpClient http, ChannelMetrics channel, string apiKey, CancellationToken ct)
    {
        try
        {
            var handle = NormalizeHandle(channel.ChannelHandle, channel.ChannelUrl);
            var queryParam = !string.IsNullOrWhiteSpace(channel.ExternalChannelId) && channel.ExternalChannelId.StartsWith("UC", StringComparison.OrdinalIgnoreCase)
                ? $"id={Uri.EscapeDataString(channel.ExternalChannelId)}"
                : !string.IsNullOrWhiteSpace(handle)
                    ? $"forHandle={Uri.EscapeDataString(handle)}"
                    : string.Empty;

            if (string.IsNullOrEmpty(queryParam))
                return false;

            var url = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics,contentDetails&{queryParam}&key={Uri.EscapeDataString(apiKey)}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return false;

            var item = items[0];
            if (item.TryGetProperty("id", out var idEl))
                channel.ExternalChannelId = idEl.GetString() ?? channel.ExternalChannelId;

            if (item.TryGetProperty("snippet", out var snippet))
            {
                if (string.IsNullOrWhiteSpace(channel.ChannelName) && snippet.TryGetProperty("title", out var titleEl))
                    channel.ChannelName = titleEl.GetString() ?? channel.ChannelName;
                if (snippet.TryGetProperty("thumbnails", out var thumbs) &&
                    thumbs.TryGetProperty("default", out var defThumb) &&
                    defThumb.TryGetProperty("url", out var avatarEl))
                {
                    channel.AvatarUrl = avatarEl.GetString() ?? channel.AvatarUrl;
                }
            }

            if (item.TryGetProperty("statistics", out var stats))
            {
                if (stats.TryGetProperty("subscriberCount", out var subsEl) &&
                    long.TryParse(subsEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var subs))
                {
                    channel.FollowersOrSubscribers = subs;
                }
                if (stats.TryGetProperty("videoCount", out var vidEl) &&
                    int.TryParse(vidEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var vids))
                {
                    channel.VideosOrPostsCount = vids;
                }
            }

            // Pull latest uploads for real 30-day window & engagement metrics
            var uploadsPlaylist = item.TryGetProperty("contentDetails", out var cd) &&
                cd.TryGetProperty("relatedPlaylists", out var rp) &&
                rp.TryGetProperty("uploads", out var up)
                ? up.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(uploadsPlaylist))
            {
                await PopulateRecentVideosApiV3Async(http, channel, uploadsPlaylist, apiKey, ct);
            }

            channel.DataSource = "YouTubeApiV3";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube Data API v3 lookup failed for {Handle}; falling back to public feed.", channel.ChannelHandle);
            return false;
        }
    }

    private async Task PopulateRecentVideosApiV3Async(HttpClient http, ChannelMetrics channel, string playlistId, string apiKey, CancellationToken ct)
    {
        var listUrl = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&maxResults=15&playlistId={Uri.EscapeDataString(playlistId)}&key={Uri.EscapeDataString(apiKey)}";
        using var listResp = await http.GetAsync(listUrl, ct);
        if (!listResp.IsSuccessStatusCode) return;

        using var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync(ct));
        if (!listDoc.RootElement.TryGetProperty("items", out var items)) return;

        var videoIds = new List<string>();
        foreach (var el in items.EnumerateArray())
        {
            if (el.TryGetProperty("contentDetails", out var cd) && cd.TryGetProperty("videoId", out var vid))
            {
                var id = vid.GetString();
                if (!string.IsNullOrWhiteSpace(id)) videoIds.Add(id);
            }
        }
        if (videoIds.Count == 0) return;

        var vidsUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics&id={Uri.EscapeDataString(string.Join(',', videoIds))}&key={Uri.EscapeDataString(apiKey)}";
        using var vidsResp = await http.GetAsync(vidsUrl, ct);
        if (!vidsResp.IsSuccessStatusCode) return;

        using var vidsDoc = JsonDocument.Parse(await vidsResp.Content.ReadAsStringAsync(ct));
        if (!vidsDoc.RootElement.TryGetProperty("items", out var vItems)) return;

        var recent = new List<RecentVideoItem>();
        long totalViews = 0;
        long totalLikes = 0;
        long totalComments = 0;
        var cutoff = DateTime.UtcNow.AddDays(-30);
        long viewsLast30 = 0;

        foreach (var v in vItems.EnumerateArray())
        {
            var id = v.GetProperty("id").GetString() ?? "";
            var sn = v.GetProperty("snippet");
            var st = v.GetProperty("statistics");
            var title = sn.GetProperty("title").GetString() ?? "";
            var published = sn.TryGetProperty("publishedAt", out var pubEl) &&
                DateTime.TryParse(pubEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt)
                ? dt
                : DateTime.UtcNow;
            var views = st.TryGetProperty("viewCount", out var vc) && long.TryParse(vc.GetString(), out var vn) ? vn : 0;
            var likes = st.TryGetProperty("likeCount", out var lc) && long.TryParse(lc.GetString(), out var ln) ? ln : 0;
            var comments = st.TryGetProperty("commentCount", out var cc) && long.TryParse(cc.GetString(), out var cn) ? cn : 0;
            var desc = sn.TryGetProperty("description", out var de) ? de.GetString() ?? "" : "";
            var thumb = $"https://i.ytimg.com/vi/{id}/hqdefault.jpg";

            totalViews += views;
            totalLikes += likes;
            totalComments += comments;
            if (published >= cutoff) viewsLast30 += views;

            recent.Add(new RecentVideoItem
            {
                VideoId = id,
                Title = title,
                Url = $"https://www.youtube.com/watch?v={id}",
                ThumbnailUrl = thumb,
                PublishedAt = published,
                Views = views,
                DescriptionSnippet = desc.Length > 180 ? desc[..180] + "…" : desc
            });
        }

        channel.RecentVideos = recent;
        channel.TotalViewsLast30Days = viewsLast30 > 0 ? viewsLast30 : totalViews;
        if (totalViews > 0)
        {
            channel.EngagementRatePercentage = Math.Round(((totalLikes + totalComments) / (double)totalViews) * 100.0, 2);
        }
    }

    private async Task<bool> TryEnrichViaPublicFeedAsync(HttpClient http, ChannelMetrics channel, CancellationToken ct)
    {
        try
        {
            // 1. Resolve UC channel ID if needed from https://www.youtube.com/@handle
            if (string.IsNullOrWhiteSpace(channel.ExternalChannelId) || !channel.ExternalChannelId.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
            {
                var targetUrl = ResolveChannelWebUrl(channel);
                if (!string.IsNullOrWhiteSpace(targetUrl))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                    req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
                    using var resp = await http.SendAsync(req, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        var html = await resp.Content.ReadAsStringAsync(ct);
                        var idMatch = ExternalIdRegex().Match(html);
                        if (!idMatch.Success) idMatch = CanonicalChannelIdRegex().Match(html);
                        if (idMatch.Success)
                        {
                            channel.ExternalChannelId = idMatch.Groups[1].Value;
                        }

                        var avatarMatch = OgImageRegex().Match(html);
                        if (avatarMatch.Success)
                        {
                            channel.AvatarUrl = WebUtility.HtmlDecode(avatarMatch.Groups[1].Value);
                        }

                        var titleMatch = OgTitleRegex().Match(html);
                        if (string.IsNullOrWhiteSpace(channel.ChannelName) && titleMatch.Success)
                        {
                            channel.ChannelName = WebUtility.HtmlDecode(titleMatch.Groups[1].Value);
                        }

                        var subMatch = SubscriberTextRegex().Match(html);
                        if (subMatch.Success)
                        {
                            var parsedSubs = ParseCompactNumber(subMatch.Groups[1].Value);
                            if (parsedSubs > 0) channel.FollowersOrSubscribers = parsedSubs;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(channel.ExternalChannelId) || !channel.ExternalChannelId.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
                return false;

            // 2. Fetch official public Atom feed for real recent videos & per-video view statistics
            var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channel.ExternalChannelId)}";
            using var feedResp = await http.GetAsync(feedUrl, ct);
            if (!feedResp.IsSuccessStatusCode)
                return false;

            var xmlText = await feedResp.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xmlText);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
            XNamespace media = "http://search.yahoo.com/mrss/";

            var feedTitle = doc.Root?.Element(atom + "title")?.Value;
            if (string.IsNullOrWhiteSpace(channel.ChannelName) && !string.IsNullOrWhiteSpace(feedTitle))
                channel.ChannelName = feedTitle;

            var entries = doc.Root?.Elements(atom + "entry").ToList() ?? [];
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var recent = new List<RecentVideoItem>();
            long viewsLast30 = 0;
            long recentSum = 0;

            foreach (var entry in entries)
            {
                var vid = entry.Element(yt + "videoId")?.Value ?? string.Empty;
                var title = entry.Element(atom + "title")?.Value ?? string.Empty;
                var link = entry.Elements(atom + "link").FirstOrDefault()?.Attribute("href")?.Value
                           ?? $"https://www.youtube.com/watch?v={vid}";
                var pubStr = entry.Element(atom + "published")?.Value;
                var pub = DateTime.TryParse(pubStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt)
                    ? dt
                    : DateTime.UtcNow;

                var mediaGroup = entry.Element(media + "group");
                var thumb = mediaGroup?.Element(media + "thumbnail")?.Attribute("url")?.Value
                            ?? (!string.IsNullOrWhiteSpace(vid) ? $"https://i.ytimg.com/vi/{vid}/hqdefault.jpg" : "");
                var desc = mediaGroup?.Element(media + "description")?.Value ?? string.Empty;
                var viewsAttr = mediaGroup?.Element(media + "community")?.Element(media + "statistics")?.Attribute("views")?.Value;
                var views = long.TryParse(viewsAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vCount) ? vCount : 0;

                recentSum += views;
                if (pub >= cutoff) viewsLast30 += views;

                if (!string.IsNullOrWhiteSpace(vid))
                {
                    recent.Add(new RecentVideoItem
                    {
                        VideoId = vid,
                        Title = title,
                        Url = link,
                        ThumbnailUrl = thumb,
                        PublishedAt = pub,
                        Views = views,
                        DescriptionSnippet = desc.Length > 180 ? desc[..180] + "…" : desc
                    });
                }
            }

            channel.RecentVideos = recent;
            if (recent.Count > 0 && channel.VideosOrPostsCount < recent.Count)
                channel.VideosOrPostsCount = recent.Count;

            channel.TotalViewsLast30Days = viewsLast30 > 0 ? viewsLast30 : recentSum;
            channel.DataSource = "YouTubeRSS";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyless YouTube RSS fetch failed for {Handle}.", channel.ChannelHandle);
            return false;
        }
    }

    private static string ResolveChannelWebUrl(ChannelMetrics channel)
    {
        if (!string.IsNullOrWhiteSpace(channel.ChannelUrl) && Uri.TryCreate(channel.ChannelUrl, UriKind.Absolute, out _))
            return channel.ChannelUrl.Trim();
        var handle = NormalizeHandle(channel.ChannelHandle, channel.ChannelUrl);
        return string.IsNullOrWhiteSpace(handle) ? string.Empty : $"https://www.youtube.com/{handle}";
    }

    private static string NormalizeHandle(string handle, string url)
    {
        var candidate = handle.Trim();
        if (string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(url))
        {
            var idx = url.IndexOf('@');
            if (idx >= 0) candidate = url[idx..].Split('/', '?', '#')[0];
        }
        if (string.IsNullOrWhiteSpace(candidate)) return string.Empty;
        return candidate.StartsWith('@') ? candidate : $"@{candidate}";
    }

    internal static long ParseCompactNumber(string text)
    {
        var clean = text.Replace(",", "", StringComparison.Ordinal).Trim();
        var m = CompactNumberRegex().Match(clean);
        if (!m.Success) return 0;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return 0;

        var suffix = m.Groups[2].Value.ToUpperInvariant();
        return suffix switch
        {
            "K" => (long)Math.Round(num * 1_000),
            "M" => (long)Math.Round(num * 1_000_000),
            "B" => (long)Math.Round(num * 1_000_000_000),
            _ => (long)Math.Round(num)
        };
    }

    [GeneratedRegex("\"externalId\"\\s*:\\s*\"(UC[a-zA-Z0-9_-]{20,})\"")]
    private static partial Regex ExternalIdRegex();

    [GeneratedRegex("youtube\\.com/channel/(UC[a-zA-Z0-9_-]{20,})")]
    private static partial Regex CanonicalChannelIdRegex();

    [GeneratedRegex("<meta\\s+property=\"og:image\"\\s+content=\"([^\"]+)\"")]
    private static partial Regex OgImageRegex();

    [GeneratedRegex("<meta\\s+property=\"og:title\"\\s+content=\"([^\"]+)\"")]
    private static partial Regex OgTitleRegex();

    [GeneratedRegex("\"subscriberCountText\"\\s*:\\s*\\{\"simpleText\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex SubscriberTextRegex();

    [GeneratedRegex("(\\d+(?:\\.\\d+)?)\\s*([KMB])?", RegexOptions.IgnoreCase)]
    private static partial Regex CompactNumberRegex();
}
