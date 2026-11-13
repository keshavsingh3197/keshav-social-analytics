using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// Resolves any user-provided handle, channel URL, or search query live and generates
/// zero-manual metrics, niche RPM estimation, revenue projection, and tailored growth recommendations.
/// Strictly read-only against owner/admin databases so private ecosystem channels are never exposed.
/// </summary>
public sealed class ChannelLookupService
{
    private readonly SocialConfigService _configService;
    private readonly YouTubeLiveFetchService _ytFetch;
    private readonly RevenueCalculatorService _revenueCalculator;
    private readonly GrowthAdvisorService _advisorService;

    public ChannelLookupService(
        SocialConfigService configService,
        YouTubeLiveFetchService ytFetch,
        RevenueCalculatorService revenueCalculator,
        GrowthAdvisorService advisorService)
    {
        _configService = configService;
        _ytFetch = ytFetch;
        _revenueCalculator = revenueCalculator;
        _advisorService = advisorService;
    }

    public async Task<List<ChannelSearchCandidate>> SearchAsync(
        string query,
        SocialPlatform platform = SocialPlatform.YouTube,
        int maxResults = 6,
        CancellationToken ct = default)
    {
        var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
        return await _ytFetch.SearchChannelsAsync(query, apiKey, platform, maxResults, ct);
    }

    public async Task<PublicChannelAnalysisResult> AnalyzeAsync(
        PublicChannelLookupRequest request,
        CancellationToken ct = default)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Please enter a channel @handle, URL, or channel name.");

        var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
        var earningsConfig = await _configService.GetEarningsConfigAsync();
        var growthConfig = await _advisorService.GetGrowthConfigAsync();
        var allSuggestions = await _advisorService.GetAllSuggestionsAsync();

        var channel = YouTubeLiveFetchService.CreateProbeChannel(query, request.Platform);
        var resolvedLive = false;

        if (request.Platform == SocialPlatform.YouTube)
        {
            resolvedLive = await _ytFetch.EnrichChannelAsync(channel, apiKey, ct);
            if (resolvedLive && channel.FollowersOrSubscribers <= 0)
            {
                var subHits = await _ytFetch.SearchChannelsAsync(query, apiKey, SocialPlatform.YouTube, maxResults: 3, ct);
                var matchedHit = subHits.FirstOrDefault(h =>
                    (!string.IsNullOrWhiteSpace(channel.ExternalChannelId) && string.Equals(h.ExternalChannelId, channel.ExternalChannelId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(channel.ChannelHandle) && string.Equals(h.ChannelHandle, channel.ChannelHandle, StringComparison.OrdinalIgnoreCase)))
                    ?? subHits.FirstOrDefault();
                if (matchedHit is not null)
                {
                    if (matchedHit.FollowersOrSubscribers > 0)
                        channel.FollowersOrSubscribers = matchedHit.FollowersOrSubscribers;
                    if (channel.VideosOrPostsCount <= 0 && matchedHit.VideoCount > 0)
                        channel.VideosOrPostsCount = matchedHit.VideoCount;
                    if (string.IsNullOrWhiteSpace(channel.AvatarUrl) && !string.IsNullOrWhiteSpace(matchedHit.AvatarUrl))
                        channel.AvatarUrl = matchedHit.AvatarUrl;
                }
            }
            if (!resolvedLive)
            {
                var hits = await _ytFetch.SearchChannelsAsync(query, apiKey, SocialPlatform.YouTube, maxResults: 1, ct);
                if (hits.Count > 0)
                {
                    var top = hits[0];
                    channel.ExternalChannelId = top.ExternalChannelId;
                    channel.ChannelName = top.ChannelName;
                    channel.ChannelHandle = top.ChannelHandle;
                    channel.AvatarUrl = top.AvatarUrl;
                    channel.ChannelUrl = top.ChannelUrl;
                    channel.FollowersOrSubscribers = top.FollowersOrSubscribers;
                    resolvedLive = await _ytFetch.EnrichChannelAsync(channel, apiKey, ct);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(channel.ChannelName))
        {
            channel.ChannelName = channel.ChannelHandle.TrimStart('@');
        }

        // Infer niche and assign realistic benchmark defaults for fields unavailable from keyless RSS
        var detectedNiche = DetectNiche(channel, request.Niche, earningsConfig);
        channel.Niche = detectedNiche.Name;
        channel.RpmUsd = detectedNiche.SuggestedRpmUsd > 0 ? detectedNiche.SuggestedRpmUsd : 3.2;

        if (channel.TotalViewsLast30Days <= 0)
        {
            var bench = earningsConfig.PlatformBenchmarks
                .FirstOrDefault(b => b.Platform.Equals(channel.Platform.ToString(), StringComparison.OrdinalIgnoreCase));
            channel.TotalViewsLast30Days = bench?.DefaultMonthlyViews ?? 25_000;
        }

        if (channel.EngagementRatePercentage <= 0)
        {
            // Derive a sensible estimated engagement ratio from recent view-to-subscriber velocity when RSS-only
            var ratio = channel.FollowersOrSubscribers > 0
                ? (double)channel.TotalViewsLast30Days / channel.FollowersOrSubscribers
                : 0.4;
            channel.EngagementRatePercentage = Math.Round(Math.Clamp(3.2 + Math.Min(4.0, ratio * 2.5), 2.5, 9.5), 1);
        }

        if (channel.AverageViewDurationMinutes <= 0)
        {
            channel.AverageViewDurationMinutes = channel.Platform == SocialPlatform.YouTube ? 4.4 : 1.6;
        }

        if (channel.GrowthRateMonthlyPercentage <= 0 && channel.RecentVideos.Count > 0)
        {
            var recentWeekViews = channel.RecentVideos
                .Where(v => v.PublishedAt >= DateTime.UtcNow.AddDays(-7))
                .Sum(v => v.Views);
            var paceShare = channel.TotalViewsLast30Days > 0
                ? (recentWeekViews * 4.0) / channel.TotalViewsLast30Days
                : 1.0;
            channel.GrowthRateMonthlyPercentage = Math.Round(Math.Clamp((paceShare - 0.7) * 18.0, 2.5, 28.0), 1);
        }

        channel.EstimatedMonthlyRevenue = Math.Round((channel.TotalViewsLast30Days / 1000.0) * channel.RpmUsd, 2);
        channel.LastUpdated = DateTime.UtcNow;

        var sponsorshipEstimate = channel.FollowersOrSubscribers >= 100_000 ? 1200m
            : channel.FollowersOrSubscribers >= 25_000 ? 450m
            : channel.FollowersOrSubscribers >= 5_000 ? 180m
            : 0m;

        var earnings = await _revenueCalculator.CalculateEarningsAsync(new EarningsCalculationRequest
        {
            Platform = channel.Platform,
            Niche = channel.Niche,
            MonthlyViews = channel.TotalViewsLast30Days,
            EstimatedRpm = (decimal)channel.RpmUsd,
            MonthlySponsorshipDeals = sponsorshipEstimate > 0 ? 1 : 0,
            AvgSponsorshipRate = sponsorshipEstimate,
            AffiliateConversionRatePercentage = 0.4m,
            AvgAffiliatePayout = 15m
        });

        var siteConfig = await _configService.GetSiteConfigAsync();
        var audit = GrowthAdvisorService.BuildAuditResult(channel, channel.ChannelHandle, channel.Platform, growthConfig, allSuggestions, siteConfig);
        var insights = BuildAutoInsights(channel, resolvedLive);

        return new PublicChannelAnalysisResult
        {
            Channel = channel,
            Earnings = earnings,
            Audit = audit,
            AutoInsights = insights,
            ResolvedLive = resolvedLive,
            ResolutionSummary = resolvedLive
                ? $"Live verified via {channel.DataSource} ({channel.RecentVideos.Count} recent uploads analyzed)."
                : $"Generated baseline projection for {channel.ChannelHandle} using platform benchmarks."
        };
    }

    private static NichePreset DetectNiche(ChannelMetrics ch, string? requestedNiche, EarningsConfig config)
    {
        if (!string.IsNullOrWhiteSpace(requestedNiche))
        {
            var explicitMatch = config.Niches.FirstOrDefault(n =>
                n.Name.Contains(requestedNiche, StringComparison.OrdinalIgnoreCase));
            if (explicitMatch is not null) return explicitMatch;
        }

        var haystack = $"{ch.ChannelName} {ch.ChannelHandle} {string.Join(' ', ch.RecentVideos.Select(v => v.Title))}"
            .ToLowerInvariant();

        if (ContainsAny(haystack, "finance", "invest", "stock", "money", "crypto", "tax", "business"))
            return FindOrFallback(config, "Finance", 8.0);
        if (ContainsAny(haystack, "bhakti", "shiv", "krishna", "mantra", "aarti", "temple", "katha", "panchang", "devotional"))
            return FindOrFallback(config, "Devotional", 2.1);
        if (ContainsAny(haystack, "animat", "story", "3d", "2d", "cartoon", "blender"))
            return FindOrFallback(config, "Animation", 3.5);
        if (ContainsAny(haystack, "exam", "study", "upsc", "math", "science", "course", "tutorial"))
            return FindOrFallback(config, "Education", 2.6);
        if (ContainsAny(haystack, "code", "ai", "tech", "software", "review", "unboxing", "gadget", "dev"))
            return FindOrFallback(config, "Tech", 5.2);

        return config.Niches.FirstOrDefault() ?? new NichePreset
        {
            Id = "default",
            Name = string.IsNullOrWhiteSpace(ch.Niche) ? "General Creator" : ch.Niche,
            SuggestedRpmUsd = 3.2
        };
    }

    private static NichePreset FindOrFallback(EarningsConfig cfg, string keyword, double defaultRpm) =>
        cfg.Niches.FirstOrDefault(n => n.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        ?? new NichePreset { Id = keyword.ToLowerInvariant(), Name = keyword, SuggestedRpmUsd = defaultRpm };

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.Ordinal));

    private static List<string> BuildAutoInsights(ChannelMetrics ch, bool resolvedLive)
    {
        var insights = new List<string>();
        if (resolvedLive && ch.RecentVideos.Count > 0)
        {
            var avgViews = (long)Math.Round(ch.RecentVideos.Average(v => v.Views));
            var top = ch.RecentVideos.OrderByDescending(v => v.Views).First();
            insights.Add($"Top recent video: \"{top.Title}\" with {top.Views:N0} views (channel recent avg: {avgViews:N0}/video).");

            var spanDays = Math.Max(1.0, (ch.RecentVideos.Max(v => v.PublishedAt) - ch.RecentVideos.Min(v => v.PublishedAt)).TotalDays);
            var perWeek = ch.RecentVideos.Count / (spanDays / 7.0);
            insights.Add($"Observed publishing velocity: ~{perWeek:F1} uploads/week across latest {ch.RecentVideos.Count} videos.");
        }

        if (ch.FollowersOrSubscribers > 0 && ch.TotalViewsLast30Days > 0)
        {
            var ratio = (double)ch.TotalViewsLast30Days / ch.FollowersOrSubscribers;
            insights.Add($"30-day views-to-subscriber momentum ratio: {ratio:P0} ({(ratio >= 0.35 ? "strong discovery reach" : "opportunity to widen browse/Shorts reach")}).");
        }

        insights.Add($"Detected niche \"{ch.Niche}\" places estimated AdSense baseline near ${ch.RpmUsd:F2} RPM before sponsorships & affiliate funnels.");
        return insights;
    }
}
