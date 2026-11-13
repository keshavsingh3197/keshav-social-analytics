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
                await PopulateRecentVideosApiV3Async(http, channel, uploadsPlaylist, apiKey, bearerToken: null, ct);
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

    public async Task<bool> EnrichWithOAuthTokenAsync(ChannelMetrics channel, string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return false;

        try
        {
            var http = _httpFactory.CreateClient("LiveYouTube");
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics,contentDetails&mine=true");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return false;

            var item = items[0];
            ApplyChannelItem(channel, item);

            var uploadsPlaylist = item.TryGetProperty("contentDetails", out var cd) &&
                cd.TryGetProperty("relatedPlaylists", out var rp) &&
                rp.TryGetProperty("uploads", out var up)
                ? up.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(uploadsPlaylist))
            {
                await PopulateRecentVideosApiV3Async(http, channel, uploadsPlaylist, apiKey: null, bearerToken: accessToken, ct);
            }

            channel.DataSource = "YouTubeOAuth";
            channel.IsVerifiedLive = true;
            channel.LastSyncedAt = DateTime.UtcNow;
            channel.IsActive = true;
            if (channel.TotalViewsLast30Days > 0 && channel.RpmUsd > 0)
            {
                channel.EstimatedMonthlyRevenue = Math.Round((channel.TotalViewsLast30Days / 1000.0) * channel.RpmUsd, 2);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube OAuth enrichment failed for channel {Handle}.", channel.ChannelHandle);
            return false;
        }
    }

    private static void ApplyChannelItem(ChannelMetrics channel, JsonElement item)
    {
        if (item.TryGetProperty("id", out var idEl) && !string.IsNullOrWhiteSpace(idEl.GetString()))
            channel.ExternalChannelId = idEl.GetString()!;

        if (item.TryGetProperty("snippet", out var snippet))
        {
            var title = snippet.TryGetProperty("title", out var t) ? t.GetString() : null;
            var custom = snippet.TryGetProperty("customUrl", out var cu) ? cu.GetString() : null;
            if (!string.IsNullOrWhiteSpace(title)) channel.ChannelName = title;
            if (!string.IsNullOrWhiteSpace(custom))
                channel.ChannelHandle = custom.StartsWith('@') ? custom : $"@{custom}";

            if (snippet.TryGetProperty("thumbnails", out var thumbs) &&
                (thumbs.TryGetProperty("high", out var thumbObj) || thumbs.TryGetProperty("default", out thumbObj)) &&
                thumbObj.TryGetProperty("url", out var tUrl))
            {
                channel.AvatarUrl = tUrl.GetString() ?? channel.AvatarUrl;
            }
        }

        if (string.IsNullOrWhiteSpace(channel.ChannelUrl))
        {
            channel.ChannelUrl = !string.IsNullOrWhiteSpace(channel.ChannelHandle)
                ? $"https://www.youtube.com/{channel.ChannelHandle}"
                : $"https://www.youtube.com/channel/{channel.ExternalChannelId}";
        }

        if (item.TryGetProperty("statistics", out var stats))
        {
            if (stats.TryGetProperty("subscriberCount", out var subEl) &&
                long.TryParse(subEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var subs))
            {
                channel.FollowersOrSubscribers = subs;
            }
            if (stats.TryGetProperty("videoCount", out var vidEl) &&
                int.TryParse(vidEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var vids))
            {
                channel.VideosOrPostsCount = vids;
            }
        }
    }

    private async Task PopulateRecentVideosApiV3Async(
        HttpClient http,
        ChannelMetrics channel,
        string playlistId,
        string? apiKey,
        string? bearerToken,
        CancellationToken ct)
    {
        var keySuffix = !string.IsNullOrWhiteSpace(apiKey) ? $"&key={Uri.EscapeDataString(apiKey)}" : string.Empty;
        var listUrl = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&maxResults=15&playlistId={Uri.EscapeDataString(playlistId)}{keySuffix}";
        using var listReq = new HttpRequestMessage(HttpMethod.Get, listUrl);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            listReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        using var listResp = await http.SendAsync(listReq, ct);
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

        var vidsUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics&id={Uri.EscapeDataString(string.Join(',', videoIds))}{keySuffix}";
        using var vidsReq = new HttpRequestMessage(HttpMethod.Get, vidsUrl);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            vidsReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        using var vidsResp = await http.SendAsync(vidsReq, ct);
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

                        var parsedSubs = ExtractSubscriberCountFromHtml(html);
                        if (parsedSubs > 0) channel.FollowersOrSubscribers = parsedSubs;
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

    /// <summary>
    /// Searches public YouTube channels by handle, URL, channel ID, or keyword using
    /// YouTube Data API v3 when an API key is configured, with automatic keyless YouTube search
    /// &amp; handle-page fallback.
    /// </summary>
    public async Task<List<ChannelSearchCandidate>> SearchChannelsAsync(
        string query,
        string? apiKey,
        SocialPlatform platform = SocialPlatform.YouTube,
        int maxResults = 6,
        CancellationToken ct = default)
    {
        var raw = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return [];

        maxResults = Math.Clamp(maxResults, 1, 10);

        if (platform != SocialPlatform.YouTube)
        {
            var normalizedHandle = NormalizeSocialHandle(raw);
            return
            [
                new ChannelSearchCandidate
                {
                    ExternalChannelId = normalizedHandle.TrimStart('@').ToLowerInvariant(),
                    ChannelName = normalizedHandle.TrimStart('@'),
                    ChannelHandle = normalizedHandle,
                    Platform = platform,
                    ChannelUrl = BuildSocialProfileUrl(platform, normalizedHandle),
                    DataSource = "ProfileHandle",
                    IsVerified = false
                }
            ];
        }

        var http = _httpFactory.CreateClient("LiveYouTube");
        var results = new List<ChannelSearchCandidate>();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            results.AddRange(await SearchViaApiV3Async(http, raw, apiKey, maxResults, ct));
            if (results.Count > 0) return results;
        }

        // Keyless exact handle / URL / UC-ID lookup first when input looks like a direct identifier
        if (LooksLikeHandleOrChannelUrl(raw))
        {
            var probe = CreateProbeChannel(raw, SocialPlatform.YouTube);
            if (await TryEnrichViaPublicFeedAsync(http, probe, ct))
            {
                results.Add(new ChannelSearchCandidate
                {
                    ExternalChannelId = probe.ExternalChannelId,
                    ChannelName = string.IsNullOrWhiteSpace(probe.ChannelName) ? probe.ChannelHandle : probe.ChannelName,
                    ChannelHandle = probe.ChannelHandle,
                    Platform = SocialPlatform.YouTube,
                    AvatarUrl = probe.AvatarUrl,
                    FollowersOrSubscribers = probe.FollowersOrSubscribers,
                    VideoCount = probe.VideosOrPostsCount,
                    ChannelUrl = probe.ChannelUrl,
                    DataSource = "YouTubePublic",
                    IsVerified = true
                });
                return results;
            }
        }

        // Keyless public channel search via YouTube search results page (sp=EgIQAg%3D%3D filters Channels)
        results.AddRange(await SearchViaYouTubeResultsPageAsync(http, raw, maxResults, ct));
        if (results.Count > 0) return results;

        // Final graceful handle probe (e.g. "keshavsinghworld" -> "@keshavsinghworld")
        var fallbackProbe = CreateProbeChannel(raw, SocialPlatform.YouTube);
        if (await TryEnrichViaPublicFeedAsync(http, fallbackProbe, ct))
        {
            results.Add(new ChannelSearchCandidate
            {
                ExternalChannelId = fallbackProbe.ExternalChannelId,
                ChannelName = string.IsNullOrWhiteSpace(fallbackProbe.ChannelName) ? fallbackProbe.ChannelHandle : fallbackProbe.ChannelName,
                ChannelHandle = fallbackProbe.ChannelHandle,
                Platform = SocialPlatform.YouTube,
                AvatarUrl = fallbackProbe.AvatarUrl,
                FollowersOrSubscribers = fallbackProbe.FollowersOrSubscribers,
                VideoCount = fallbackProbe.VideosOrPostsCount,
                ChannelUrl = fallbackProbe.ChannelUrl,
                DataSource = "YouTubePublic",
                IsVerified = true
            });
        }

        return results;
    }

    internal static ChannelMetrics CreateProbeChannel(string queryOrHandle, SocialPlatform platform)
    {
        var raw = queryOrHandle.Trim();
        var externalId = ExtractChannelId(raw);
        var handle = platform == SocialPlatform.YouTube
            ? NormalizeHandle(raw, raw)
            : NormalizeSocialHandle(raw);

        if (!string.IsNullOrWhiteSpace(externalId) && handle.StartsWith("@channel/", StringComparison.OrdinalIgnoreCase))
            handle = string.Empty;

        var url = Uri.TryCreate(raw, UriKind.Absolute, out var parsed) &&
                  (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            ? raw
            : platform == SocialPlatform.YouTube
                ? (!string.IsNullOrWhiteSpace(externalId)
                    ? $"https://www.youtube.com/channel/{externalId}"
                    : $"https://www.youtube.com/{handle}")
                : BuildSocialProfileUrl(platform, handle);

        return new ChannelMetrics
        {
            ChannelHandle = string.IsNullOrWhiteSpace(handle) ? externalId : handle,
            ExternalChannelId = externalId,
            Platform = platform,
            ChannelUrl = url,
            DataSource = platform == SocialPlatform.YouTube ? "YouTubePublic" : "PlatformBenchmark",
            RpmUsd = 3.2
        };
    }

    private async Task<List<ChannelSearchCandidate>> SearchViaApiV3Async(
        HttpClient http,
        string query,
        string apiKey,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            var foundIds = new List<string>();
            var directUc = ExtractChannelId(query);
            if (!string.IsNullOrWhiteSpace(directUc))
            {
                foundIds.Add(directUc);
            }
            else if (query.StartsWith('@') || query.Contains("youtube.com/@", StringComparison.OrdinalIgnoreCase))
            {
                var h = NormalizeHandle(query, query).TrimStart('@');
                var exactUrl = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics&forHandle={Uri.EscapeDataString(h)}&key={Uri.EscapeDataString(apiKey)}";
                using var exactResp = await http.GetAsync(exactUrl, ct);
                if (exactResp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await exactResp.Content.ReadAsStringAsync(ct));
                    var exact = ParseApiV3Candidates(doc.RootElement);
                    if (exact.Count > 0) return exact;
                }
            }

            if (foundIds.Count == 0)
            {
                var searchUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=channel&maxResults={maxResults}&q={Uri.EscapeDataString(query)}&key={Uri.EscapeDataString(apiKey)}";
                using var sResp = await http.GetAsync(searchUrl, ct);
                if (!sResp.IsSuccessStatusCode) return [];

                using var sDoc = JsonDocument.Parse(await sResp.Content.ReadAsStringAsync(ct));
                if (sDoc.RootElement.TryGetProperty("items", out var sItems))
                {
                    foreach (var item in sItems.EnumerateArray())
                    {
                        if (item.TryGetProperty("snippet", out var sn) &&
                            sn.TryGetProperty("channelId", out var cidEl) &&
                            !string.IsNullOrWhiteSpace(cidEl.GetString()))
                        {
                            foundIds.Add(cidEl.GetString()!);
                        }
                    }
                }
            }

            if (foundIds.Count == 0) return [];

            var detailsUrl = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics&id={Uri.EscapeDataString(string.Join(',', foundIds.Distinct()))}&key={Uri.EscapeDataString(apiKey)}";
            using var dResp = await http.GetAsync(detailsUrl, ct);
            if (!dResp.IsSuccessStatusCode) return [];

            using var dDoc = JsonDocument.Parse(await dResp.Content.ReadAsStringAsync(ct));
            return ParseApiV3Candidates(dDoc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube Data API v3 channel search failed for '{Query}'.", query);
            return [];
        }
    }

    private static List<ChannelSearchCandidate> ParseApiV3Candidates(JsonElement root)
    {
        var list = new List<ChannelSearchCandidate>();
        if (!root.TryGetProperty("items", out var items)) return list;

        foreach (var item in items.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            var sn = item.TryGetProperty("snippet", out var snEl) ? snEl : default;
            var title = sn.ValueKind == JsonValueKind.Object && sn.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
            var desc = sn.ValueKind == JsonValueKind.Object && sn.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
            var customUrl = sn.ValueKind == JsonValueKind.Object && sn.TryGetProperty("customUrl", out var cEl) ? cEl.GetString() ?? "" : "";
            var handle = !string.IsNullOrWhiteSpace(customUrl)
                ? (customUrl.StartsWith('@') ? customUrl : $"@{customUrl}")
                : id;

            var avatar = string.Empty;
            if (sn.ValueKind == JsonValueKind.Object &&
                sn.TryGetProperty("thumbnails", out var thumbs) &&
                (thumbs.TryGetProperty("medium", out var tObj) || thumbs.TryGetProperty("default", out tObj)) &&
                tObj.TryGetProperty("url", out var uEl))
            {
                avatar = uEl.GetString() ?? string.Empty;
            }

            long subs = 0;
            int videos = 0;
            if (item.TryGetProperty("statistics", out var st))
            {
                if (st.TryGetProperty("subscriberCount", out var sc))
                    long.TryParse(sc.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out subs);
                if (st.TryGetProperty("videoCount", out var vc))
                    int.TryParse(vc.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out videos);
            }

            list.Add(new ChannelSearchCandidate
            {
                ExternalChannelId = id,
                ChannelName = string.IsNullOrWhiteSpace(title) ? handle : title,
                ChannelHandle = handle,
                Platform = SocialPlatform.YouTube,
                AvatarUrl = avatar,
                FollowersOrSubscribers = subs,
                VideoCount = videos,
                Description = desc.Length > 140 ? desc[..140] + "…" : desc,
                ChannelUrl = handle.StartsWith('@') ? $"https://www.youtube.com/{handle}" : $"https://www.youtube.com/channel/{id}",
                DataSource = "YouTubeApiV3",
                IsVerified = true
            });
        }

        return list;
    }

    private async Task<List<ChannelSearchCandidate>> SearchViaYouTubeResultsPageAsync(
        HttpClient http,
        string query,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            var url = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}&sp=EgIQAg%253D%253D";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return [];

            var html = await resp.Content.ReadAsStringAsync(ct);
            return ParseSearchResultsHtml(html, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyless YouTube channel search failed for '{Query}'.", query);
            return [];
        }
    }

    internal static List<ChannelSearchCandidate> ParseSearchResultsHtml(string html, int maxResults)
    {
        var candidates = new List<ChannelSearchCandidate>();
        var marker = "var ytInitialData = ";
        var startIdx = html.IndexOf(marker, StringComparison.Ordinal);
        if (startIdx < 0)
        {
            marker = "ytInitialData\" : ";
            startIdx = html.IndexOf(marker, StringComparison.Ordinal);
        }
        if (startIdx < 0) return candidates;

        var jsonStart = html.IndexOf('{', startIdx);
        if (jsonStart < 0) return candidates;

        var jsonEnd = html.IndexOf(";</script>", jsonStart, StringComparison.Ordinal);
        if (jsonEnd <= jsonStart) return candidates;

        try
        {
            using var doc = JsonDocument.Parse(html[jsonStart..jsonEnd]);
            CollectChannelRenderers(doc.RootElement, candidates, maxResults);
        }
        catch
        {
            // Ignore malformed scraped JSON
        }

        return candidates;
    }

    private static void CollectChannelRenderers(JsonElement el, List<ChannelSearchCandidate> results, int maxResults)
    {
        if (results.Count >= maxResults) return;

        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("channelRenderer", out var ch) && ch.ValueKind == JsonValueKind.Object)
            {
                var id = ch.TryGetProperty("channelId", out var idEl) ? idEl.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(id) && !results.Any(r => r.ExternalChannelId == id))
                {
                    var title = ReadYtText(ch, "title");
                    var canonical = ch.TryGetProperty("navigationEndpoint", out var nav) &&
                                    nav.TryGetProperty("browseEndpoint", out var be) &&
                                    be.TryGetProperty("canonicalBaseUrl", out var cb)
                        ? cb.GetString() ?? ""
                        : "";

                    var handle = canonical.TrimStart('/');
                    if (!string.IsNullOrWhiteSpace(handle) && !handle.StartsWith('@') && !handle.StartsWith("channel/", StringComparison.OrdinalIgnoreCase))
                        handle = $"@{handle}";
                    else if (handle.StartsWith("channel/", StringComparison.OrdinalIgnoreCase))
                        handle = id;

                    // subscriberCountText in newer YouTube payloads often holds "@handle" while videoCountText holds subscribers!
                    var subText = ReadYtText(ch, "subscriberCountText");
                    var vidText = ReadYtText(ch, "videoCountText");
                    long subs = ParseCompactNumber(subText);
                    if (subs == 0) subs = ParseCompactNumber(vidText);
                    if (string.IsNullOrWhiteSpace(handle) && subText.StartsWith('@')) handle = subText;
                    if (string.IsNullOrWhiteSpace(handle)) handle = id;

                    var avatar = string.Empty;
                    if (ch.TryGetProperty("thumbnail", out var th) &&
                        th.TryGetProperty("thumbnails", out var thArr) &&
                        thArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in thArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("url", out var u)) avatar = u.GetString() ?? avatar;
                        }
                        if (avatar.StartsWith("//", StringComparison.Ordinal)) avatar = "https:" + avatar;
                    }

                    results.Add(new ChannelSearchCandidate
                    {
                        ExternalChannelId = id,
                        ChannelName = string.IsNullOrWhiteSpace(title) ? handle : title,
                        ChannelHandle = handle,
                        Platform = SocialPlatform.YouTube,
                        AvatarUrl = avatar,
                        FollowersOrSubscribers = subs,
                        Description = ReadYtText(ch, "descriptionSnippet"),
                        ChannelUrl = handle.StartsWith('@') ? $"https://www.youtube.com/{handle}" : $"https://www.youtube.com/channel/{id}",
                        DataSource = "YouTubeSearch",
                        IsVerified = true
                    });
                }
                return;
            }

            foreach (var prop in el.EnumerateObject())
            {
                CollectChannelRenderers(prop.Value, results, maxResults);
                if (results.Count >= maxResults) return;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                CollectChannelRenderers(item, results, maxResults);
                if (results.Count >= maxResults) return;
            }
        }
    }

    private static string ReadYtText(JsonElement parent, string propName)
    {
        if (!parent.TryGetProperty(propName, out var el)) return string.Empty;
        if (el.TryGetProperty("simpleText", out var simple)) return simple.GetString() ?? string.Empty;
        if (el.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(runs.EnumerateArray()
                .Select(r => r.TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""));
        }
        return string.Empty;
    }

    private static bool LooksLikeHandleOrChannelUrl(string raw) =>
        raw.StartsWith('@') ||
        raw.StartsWith("UC", StringComparison.Ordinal) ||
        raw.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

    private static string ExtractChannelId(string raw)
    {
        if (raw.StartsWith("UC", StringComparison.Ordinal) && raw.Length >= 22 && !raw.Contains(' '))
            return raw.Split('/', '?', '#')[0];
        var m = CanonicalChannelIdRegex().Match(raw);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static string NormalizeSocialHandle(string raw)
    {
        var clean = raw.Trim();
        if (Uri.TryCreate(clean, UriKind.Absolute, out var uri))
        {
            clean = uri.AbsolutePath.Trim('/').Split('/')[0];
        }
        clean = clean.Split(' ', '?', '#')[0].Trim('/');
        if (string.IsNullOrWhiteSpace(clean)) return "@creator";
        return clean.StartsWith('@') ? clean : $"@{clean}";
    }

    private static string BuildSocialProfileUrl(SocialPlatform platform, string handle)
    {
        var slug = handle.TrimStart('@');
        return platform switch
        {
            SocialPlatform.Instagram => $"https://www.instagram.com/{slug}",
            SocialPlatform.Facebook => $"https://www.facebook.com/{slug}",
            SocialPlatform.LinkedIn => $"https://www.linkedin.com/in/{slug}",
            SocialPlatform.TikTok => $"https://www.tiktok.com/@{slug}",
            SocialPlatform.Twitter => $"https://x.com/{slug}",
            _ => $"https://www.youtube.com/@{slug}"
        };
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

    internal static long ExtractSubscriberCountFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 0;

        var legacyMatch = SubscriberTextRegex().Match(html);
        if (legacyMatch.Success)
        {
            var parsed = ParseCompactNumber(legacyMatch.Groups[1].Value);
            if (parsed > 0) return parsed;
        }

        // Modern YouTube handle pages embed subscriber counts in pageHeaderViewModel / metadataRows
        // (e.g. "content":"1.24K subscribers" or "185 subscribers").
        var modernMatch = ModernSubscriberTextRegex().Match(html);
        if (modernMatch.Success)
        {
            var parsed = ParseCompactNumber(modernMatch.Groups[1].Value);
            if (parsed > 0) return parsed;
        }

        var accessibilityMatch = AccessibilitySubscriberRegex().Match(html);
        if (accessibilityMatch.Success)
        {
            var parsed = ParseCompactNumber(accessibilityMatch.Groups[1].Value);
            if (parsed > 0) return parsed;
        }

        return 0;
    }

    [GeneratedRegex("\"subscriberCountText\"\\s*:\\s*\\{\"simpleText\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex SubscriberTextRegex();

    [GeneratedRegex("\"(?:content|simpleText|text)\"\\s*:\\s*\"([\\d.,]+\\s*[KMB]?\\s+subscribers?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ModernSubscriberTextRegex();

    [GeneratedRegex("\"label\"\\s*:\\s*\"([\\d.,]+\\s*[KMB]?\\s+subscribers?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AccessibilitySubscriberRegex();

    [GeneratedRegex("(\\d+(?:\\.\\d+)?)\\s*([KMB])?", RegexOptions.IgnoreCase)]
    private static partial Regex CompactNumberRegex();
}
