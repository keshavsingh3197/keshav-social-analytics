using System.Text;
using System.Text.RegularExpressions;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class GrowthAdvisorService
{
    private const string SuggestionsCollection = "growth_suggestions";
    private const string ConfigCollection = "growth_config";

    private readonly HybridDataStore _store;
    private readonly AnalyticsEngineService _analyticsService;
    private readonly SocialConfigService? _configService;

    public GrowthAdvisorService(
        HybridDataStore store,
        AnalyticsEngineService analyticsService,
        SocialConfigService? configService = null)
    {
        _store = store;
        _analyticsService = analyticsService;
        _configService = configService;
    }

    public Task<List<GrowthSuggestion>> GetAllSuggestionsAsync() =>
        _store.GetListAsync(SuggestionsCollection, SocialSeedData.DefaultSuggestions);

    public async Task<GrowthSuggestion> UpsertSuggestionAsync(GrowthSuggestion suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion.Id))
            suggestion.Id = $"gs-{Guid.NewGuid():N}"[..10];

        return await _store.UpsertItemAsync(
            SuggestionsCollection,
            suggestion.Id,
            s => s.Id,
            suggestion,
            SocialSeedData.DefaultSuggestions);
    }

    public Task<bool> DeleteSuggestionAsync(string id) =>
        _store.DeleteItemAsync(SuggestionsCollection, id, s => s.Id, SocialSeedData.DefaultSuggestions);

    public Task<GrowthConfig> GetGrowthConfigAsync() =>
        _store.GetSingletonAsync(ConfigCollection, "default", SocialSeedData.DefaultGrowthConfig);

    public Task<GrowthConfig> SaveGrowthConfigAsync(GrowthConfig config)
    {
        config.Id = "default";
        config.UpdatedAt = DateTime.UtcNow;
        config.BestUploadTimes ??= new(StringComparer.OrdinalIgnoreCase);
        config.HighDemandKeywords ??= [];
        return _store.SaveSingletonAsync(ConfigCollection, "default", config);
    }

    public async Task<ChannelAuditResult> AuditChannelAsync(
        string channelHandle,
        SocialPlatform platform,
        string? callerUserId = null,
        bool isAdmin = false,
        ChannelMetrics? transientChannel = null)
    {
        var allSuggestions = await GetAllSuggestionsAsync();
        var config = await GetGrowthConfigAsync();
        var siteConfig = _configService is not null
            ? await _configService.GetSiteConfigAsync()
            : await _store.GetSingletonAsync("site_config", "default", SocialSeedData.DefaultSiteConfig);

        ChannelMetrics? matchedChannel = transientChannel;
        if (matchedChannel is null && !string.IsNullOrWhiteSpace(callerUserId) && !string.IsNullOrWhiteSpace(channelHandle))
        {
            var userChannels = await _analyticsService.GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: true);
            matchedChannel = userChannels.FirstOrDefault(c =>
                c.ChannelHandle.Equals(channelHandle, StringComparison.OrdinalIgnoreCase) ||
                c.ChannelName.Equals(channelHandle, StringComparison.OrdinalIgnoreCase));
        }

        return BuildAuditResult(matchedChannel, channelHandle, platform, config, allSuggestions, siteConfig);
    }

    public static ChannelAuditResult BuildAuditResult(
        ChannelMetrics? matchedChannel,
        string channelHandle,
        SocialPlatform platform,
        GrowthConfig config,
        List<GrowthSuggestion> allSuggestions,
        SiteConfig? siteConfig = null)
    {
        var dynamicSuggestions = BuildChannelSpecificSuggestions(matchedChannel, platform, config);
        var prioritySuggestions = dynamicSuggestions
            .Concat(allSuggestions
                .OrderByDescending(s => s.ApplicablePlatform == platform)
                .ThenBy(s => s.Impact)
                .Select(s => new GrowthSuggestion
                {
                    Id = s.Id,
                    Category = s.Category,
                    Title = s.Title,
                    Description = s.Description,
                    Impact = s.Impact,
                    ActionItem = s.ActionItem,
                    EstimatedGain = s.EstimatedGain,
                    ApplicablePlatform = s.ApplicablePlatform
                }))
            .ToList();

        double healthScore = 76.0;
        if (matchedChannel is not null)
        {
            var engScore = Math.Min(35.0, (matchedChannel.EngagementRatePercentage / Math.Max(1.0, config.TargetEngagementPercent)) * 28.0);
            var durationScore = Math.Min(30.0, (matchedChannel.AverageViewDurationMinutes / Math.Max(0.5, config.TargetAvgWatchMinutes)) * 24.0);
            var growthScore = Math.Min(25.0, Math.Max(0.0, matchedChannel.GrowthRateMonthlyPercentage) * 1.4);
            var volumeScore = Math.Min(10.0, matchedChannel.VideosOrPostsCount / 15.0);
            healthScore = Math.Round(Math.Clamp(engScore + durationScore + growthScore + volumeScore, 38.0, 99.2), 1);
        }

        var uploadTimes = BuildUploadTimes(platform, config);
        var effectiveHandle = string.IsNullOrWhiteSpace(matchedChannel?.ChannelHandle) ? channelHandle : matchedChannel.ChannelHandle;
        var effectiveChannelName = string.IsNullOrWhiteSpace(matchedChannel?.ChannelName)
            ? (string.IsNullOrWhiteSpace(effectiveHandle) ? $"{platform} Channel" : effectiveHandle)
            : matchedChannel.ChannelName;
        var effectiveNiche = string.IsNullOrWhiteSpace(matchedChannel?.Niche) ? "General" : matchedChannel.Niche;

        var (stats, videoAudits) = AnalyzeRecentVideos(matchedChannel, effectiveChannelName, effectiveNiche);
        var scorePillars = BuildScorePillars(matchedChannel, config, stats);
        var detectedIssues = BuildDetectedIssues(matchedChannel, effectiveHandle, platform, config, siteConfig, stats, videoAudits);
        var promptTemplates = BuildPromptTemplates(matchedChannel, effectiveChannelName, effectiveHandle, effectiveNiche, platform, config, siteConfig, stats, videoAudits);
        var recommendedDescription = BuildRecommendedDescriptionTemplate(effectiveChannelName, effectiveHandle, effectiveNiche, config, siteConfig);
        var recommendedPinnedComment = BuildRecommendedPinnedComment(effectiveChannelName, effectiveNiche, siteConfig);

        var snapshot = matchedChannel is null
            ? null
            : new ChannelAuditSnapshot
            {
                ChannelName = matchedChannel.ChannelName,
                ChannelHandle = matchedChannel.ChannelHandle,
                Platform = matchedChannel.Platform.ToString(),
                Niche = effectiveNiche,
                AvatarUrl = matchedChannel.AvatarUrl,
                ChannelUrl = matchedChannel.ChannelUrl,
                DataSource = matchedChannel.DataSource,
                IsVerifiedLive = matchedChannel.IsVerifiedLive,
                FollowersOrSubscribers = matchedChannel.FollowersOrSubscribers,
                TotalViewsLast30Days = matchedChannel.TotalViewsLast30Days,
                EngagementRatePercentage = matchedChannel.EngagementRatePercentage,
                AverageViewDurationMinutes = matchedChannel.AverageViewDurationMinutes,
                VideosAnalyzed = stats.VideosAnalyzed,
                ShortsCount = stats.ShortsCount,
                LongFormCount = stats.LongFormCount,
                AvgViewsPerRecentVideo = (long)Math.Round(stats.AvgViewsPerVideo),
                UploadsPerWeek = Math.Round(stats.UploadsPerWeek, 1),
                AvgTitleLength = stats.AvgTitleLength,
                VideosWithDescriptionLinks = stats.VideosWithLinks,
                EstimatedRpmUsd = matchedChannel.RpmUsd,
                EstimatedMonthlyRevenue = matchedChannel.EstimatedMonthlyRevenue
            };

        return new ChannelAuditResult
        {
            ChannelHandle = effectiveHandle,
            HealthScoreOutOf100 = healthScore,
            OverallGrade = ComputeGrade(healthScore),
            ExecutiveSummary = BuildExecutiveSummary(matchedChannel, effectiveChannelName, platform, healthScore, stats, detectedIssues),
            Snapshot = snapshot,
            ScorePillars = scorePillars,
            DetectedIssues = detectedIssues,
            VideoAudits = videoAudits,
            PromptTemplates = promptTemplates,
            RecommendedDescriptionTemplate = recommendedDescription,
            RecommendedPinnedComment = recommendedPinnedComment,
            PrioritySuggestions = prioritySuggestions,
            BestUploadTimes = uploadTimes,
            HighDemandKeywords = config.HighDemandKeywords
        };
    }

    private sealed record VideoSummaryStats(
        int VideosAnalyzed,
        int ShortsCount,
        int LongFormCount,
        double AvgViewsPerVideo,
        double UploadsPerWeek,
        int AvgTitleLength,
        int VideosWithLinks,
        VideoAuditItem? TopVideo,
        VideoAuditItem? WeakestVideo);

    private static (VideoSummaryStats Stats, List<VideoAuditItem> Items) AnalyzeRecentVideos(
        ChannelMetrics? ch,
        string channelName,
        string niche)
    {
        if (ch is null || ch.RecentVideos.Count == 0)
            return (new VideoSummaryStats(0, 0, 0, 0, 0, 0, 0, null, null), []);

        var recent = ch.RecentVideos
            .OrderByDescending(v => v.PublishedAt)
            .Take(12)
            .ToList();

        var avgViews = recent.Average(v => (double)Math.Max(0, v.Views));
        var spanDays = recent.Count >= 2
            ? Math.Max(1.0, (recent.Max(v => v.PublishedAt) - recent.Min(v => v.PublishedAt)).TotalDays)
            : 7.0;
        var uploadsPerWeek = recent.Count / Math.Max(1.0, spanDays / 7.0);

        var items = new List<VideoAuditItem>(recent.Count);
        int shortsCount = 0;
        int linksCount = 0;

        foreach (var v in recent)
        {
            var title = (v.Title ?? string.Empty).Trim();
            var snippet = v.DescriptionSnippet ?? string.Empty;
            var isShort = v.Url.Contains("/shorts/", StringComparison.OrdinalIgnoreCase)
                || title.Contains("#shorts", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains("#shorts", StringComparison.OrdinalIgnoreCase);
            if (isShort) shortsCount++;

            var hasLink = snippet.Contains("http://", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains("https://", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains(".in/", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains(".com/", StringComparison.OrdinalIgnoreCase);
            if (hasLink) linksCount++;

            var hasHook = ContainsHookSignal(title);
            var ratio = avgViews > 0 ? Math.Round(v.Views / avgViews, 2) : 1.0;

            var tag = "Solid";
            if (avgViews > 0 && v.Views >= avgViews * 1.35) tag = "Breakout";
            else if (avgViews > 0 && v.Views < avgViews * 0.65) tag = "Underperforming";

            var note = BuildVideoDiagnosticNote(title, ratio, isShort, hasLink, hasHook, tag);
            var suggestedTitle = BuildSuggestedTitleRewrite(title, channelName, niche, isShort);
            var videoPrompt = BuildSingleVideoFixPrompt(title, v.Views, avgViews, niche, channelName, isShort, hasLink, suggestedTitle);

            items.Add(new VideoAuditItem
            {
                VideoId = v.VideoId,
                Title = title,
                Url = v.Url,
                ThumbnailUrl = v.ThumbnailUrl,
                PublishedAt = v.PublishedAt,
                Views = v.Views,
                ViewRatioVsAvg = ratio,
                TitleLength = title.Length,
                IsShort = isShort,
                HasDescriptionLink = hasLink,
                HasHookOrStructure = hasHook,
                PerformanceTag = tag,
                DiagnosticNote = note,
                SuggestedTitleRewrite = suggestedTitle,
                FixPrompt = videoPrompt
            });
        }

        var avgTitleLen = items.Count > 0 ? (int)Math.Round(items.Average(i => i.TitleLength)) : 0;
        var top = items.OrderByDescending(i => i.Views).FirstOrDefault();
        var weakest = items.OrderBy(i => i.Views).FirstOrDefault();

        var stats = new VideoSummaryStats(
            VideosAnalyzed: items.Count,
            ShortsCount: shortsCount,
            LongFormCount: items.Count - shortsCount,
            AvgViewsPerVideo: avgViews,
            UploadsPerWeek: uploadsPerWeek,
            AvgTitleLength: avgTitleLen,
            VideosWithLinks: linksCount,
            TopVideo: top,
            WeakestVideo: weakest);

        return (stats, items);
    }

    private static List<AuditScorePillar> BuildScorePillars(
        ChannelMetrics? ch,
        GrowthConfig config,
        VideoSummaryStats stats)
    {
        if (ch is null)
        {
            return
            [
                new() { Key = "reach", Label = "Audience & Reach", Score = 70, Status = "Needs Work", Summary = "Analyze or connect a channel to compute live subscriber & velocity scores." },
                new() { Key = "engagement", Label = "Engagement & Watch Depth", Score = 74, Status = "Needs Work", Summary = $"Benchmark target: {config.TargetEngagementPercent:F1}% engagement & {config.TargetAvgWatchMinutes:F1}m avg view." },
                new() { Key = "consistency", Label = "Cadence & Format Balance", Score = 72, Status = "Needs Work", Summary = "Weekly consistency plus Shorts-to-Long-Form bridges drive Browse impressions." },
                new() { Key = "seo", Label = "Titles & Metadata SEO", Score = 68, Status = "Needs Work", Summary = "Keep mobile titles <=55 chars with front-loaded hooks and tracked links." },
                new() { Key = "monetization", Label = "Funnel & Revenue Capture", Score = 65, Status = "Needs Work", Summary = "Add tracked destination links in descriptions & pinned comments." }
            ];
        }

        var reachScore = ch.FollowersOrSubscribers > 0
            ? (int)Math.Clamp(Math.Round(55 + Math.Min(45, (ch.TotalViewsLast30Days / Math.Max(100.0, ch.FollowersOrSubscribers)) * 65.0)), 35, 98)
            : (ch.TotalViewsLast30Days > 0 ? 64 : 42);

        var engScore = (int)Math.Clamp(Math.Round((ch.EngagementRatePercentage / Math.Max(1.0, config.TargetEngagementPercent)) * 82.0), 30, 99);

        var cadenceBase = stats.VideosAnalyzed > 0 ? Math.Min(75, stats.UploadsPerWeek * 28.0) : Math.Min(70, ch.VideosOrPostsCount / 2.0);
        var mixBonus = stats.ShortsCount > 0 && stats.LongFormCount > 0 ? 22 : (stats.VideosAnalyzed > 0 ? 8 : 10);
        var consistencyScore = (int)Math.Clamp(Math.Round(cadenceBase + mixBonus), 32, 98);

        var linkBonus = stats.VideosAnalyzed > 0
            ? (int)Math.Round((double)stats.VideosWithLinks / stats.VideosAnalyzed * 25.0)
            : 12;
        var titleScore = stats.VideosAnalyzed == 0 ? 70
            : (stats.AvgTitleLength <= 56 ? 50 : Math.Max(22, 50 - (stats.AvgTitleLength - 56)))
              + linkBonus
              + 20;
        var seoScore = (int)Math.Clamp(titleScore, 30, 96);

        var linkRatio = stats.VideosAnalyzed > 0 ? (double)stats.VideosWithLinks / stats.VideosAnalyzed : 0.4;
        var monScore = (int)Math.Clamp(Math.Round(40 + (Math.Min(10.0, ch.RpmUsd) / 10.0) * 28.0 + linkRatio * 30.0), 28, 98);

        return
        [
            BuildPillar("reach", "Audience & Reach", reachScore,
                ch.FollowersOrSubscribers > 0
                    ? $"{ch.FollowersOrSubscribers:N0} subscribers • {ch.TotalViewsLast30Days:N0} views (30d)."
                    : $"Subscriber count hidden/0 in keyless fetch • {ch.TotalViewsLast30Days:N0} views (30d)."),
            BuildPillar("engagement", "Engagement & Watch Depth", engScore,
                $"{ch.EngagementRatePercentage:F1}% engagement vs {config.TargetEngagementPercent:F1}% target • {ch.AverageViewDurationMinutes:F1}m avg duration."),
            BuildPillar("consistency", "Cadence & Format Balance", consistencyScore,
                stats.VideosAnalyzed > 0
                    ? $"~{stats.UploadsPerWeek:F1} uploads/wk • {stats.ShortsCount} Shorts / {stats.LongFormCount} Long-form."
                    : $"{ch.VideosOrPostsCount} total uploads analyzed."),
            BuildPillar("seo", "Titles & Metadata SEO", seoScore,
                stats.VideosAnalyzed > 0
                    ? $"Avg title {stats.AvgTitleLength} chars (ideal <=55) • {stats.VideosWithLinks}/{stats.VideosAnalyzed} have links."
                    : "Optimize mobile titles <=55 chars and front-load search hooks."),
            BuildPillar("monetization", "Funnel & Revenue Capture", monScore,
                $"Est. ${ch.EstimatedMonthlyRevenue:N0}/mo @ ${ch.RpmUsd:F2} RPM • {stats.VideosWithLinks} tracked CTAs detected.")
        ];
    }

    private static AuditScorePillar BuildPillar(string key, string label, int score, string summary) => new()
    {
        Key = key,
        Label = label,
        Score = score,
        Status = score >= 78 ? "Strong" : score >= 58 ? "Needs Work" : "Critical",
        Summary = summary
    };

    private static List<ChannelIssueItem> BuildDetectedIssues(
        ChannelMetrics? ch,
        string handle,
        SocialPlatform platform,
        GrowthConfig config,
        SiteConfig? siteConfig,
        VideoSummaryStats stats,
        List<VideoAuditItem> videoAudits)
    {
        var issues = new List<ChannelIssueItem>();

        if (ch is null)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-no-channel",
                Severity = "Warning",
                Category = "Channel Connection",
                Title = "No live channel loaded yet — showing baseline strategy",
                Evidence = "Search any @handle above or connect your YouTube channel on the Dashboard for video-level diagnosis.",
                ImpactEstimate = "Unlock real CTR, Shorts mix & prompt generation",
                FixActionType = "connect-youtube",
                FixActionLabel = "Go to Dashboard Analyzer",
                FixRoute = "/dashboard",
                FixPrompt = $"Audit my {platform} channel {handle} for 30-day subscriber growth, top hook frameworks, and highest-RPM topics."
            });
            return issues;
        }

        if (platform == SocialPlatform.YouTube && ch.FollowersOrSubscribers <= 0)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-zero-subs",
                Severity = "Warning",
                Category = "Data Precision",
                Title = "Subscriber count shows 0 in public RSS/HTML snapshot",
                Evidence = $"YouTube handle page for {ch.ChannelHandle} did not expose a public subscriber string cleanly or API key/OAuth is not connected.",
                ImpactEstimate = "Accurate subs/view velocity & subscriber-rate scoring",
                FixActionType = "refresh-live",
                FixActionLabel = "Re-sync Live / Connect YouTube",
                FixRoute = "/dashboard",
                FixPrompt = $"My channel {ch.ChannelName} ({ch.ChannelHandle}) has {ch.TotalViewsLast30Days:N0} views over 30 days across {stats.VideosAnalyzed} recent videos. Give me a subscriber-conversion CTA script for the end of each video."
            });
        }

        if (stats.VideosAnalyzed > 0 && stats.VideosWithLinks == 0)
        {
            var destUrl = !string.IsNullOrWhiteSpace(siteConfig?.DefaultDestinationBaseUrl)
                ? siteConfig.DefaultDestinationBaseUrl
                : "your website or tool";
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-no-links",
                Severity = "Critical",
                Category = "Monetization & Funnel",
                Title = $"0 of {stats.VideosAnalyzed} recent videos link viewers to an external funnel",
                Evidence = $"No tracked links to {destUrl} were found in recent video descriptions. You are losing direct off-platform traffic and ad/sponsor conversions.",
                ImpactEstimate = "+15% to +40% recurring website traffic & RPM",
                FixActionType = "funnel-builder",
                FixActionLabel = "Create Funnel & Copy Description Block",
                FixRoute = "/funnel",
                FixPrompt = $"Write a high-converting 5-line YouTube description block and pinned comment for {ch.ChannelName} ({ch.Niche}) driving viewers from videos to {destUrl} without hurting retention."
            });
        }

        if (stats.VideosAnalyzed >= 3 && stats.ShortsCount == stats.VideosAnalyzed)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-all-shorts",
                Severity = "Warning",
                Category = "Shorts vs Long-Form Mix",
                Title = "100% of recent uploads are Shorts — low long-form RPM ceiling",
                Evidence = $"All {stats.VideosAnalyzed} recent videos are YouTube Shorts (est. RPM ${ch.RpmUsd:F2}). Adding 1 weekly 6-8 min video unlocked via Related Video links dramatically raises AdSense RPM.",
                ImpactEstimate = "2x-4x higher AdSense RPM",
                FixActionType = "prompt-studio",
                FixActionLabel = "Get Shorts-to-Long-Form Bridge Prompt",
                FixRoute = "",
                FixPrompt = $"Turn my top recent Short \"{stats.TopVideo?.Title ?? ch.ChannelName}\" into a structured 7-minute long-form video outline with retention beats every 45 seconds and a Shorts 'Related Video' funnel."
            });
        }
        else if (stats.VideosAnalyzed >= 3 && stats.LongFormCount == stats.VideosAnalyzed)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-no-shorts",
                Severity = "Opportunity",
                Category = "Shorts vs Long-Form Mix",
                Title = "Zero Shorts in recent uploads — missing free top-of-funnel discovery",
                Evidence = $"All {stats.VideosAnalyzed} recent uploads are standard videos. Clipping 2 punchy 35-sec vertical hooks per video with 'Related Video' links accelerates new subscriber discovery.",
                ImpactEstimate = "+25% to +50% new viewer impressions",
                FixActionType = "prompt-studio",
                FixActionLabel = "Generate Shorts Hook Prompt",
                FixRoute = "",
                FixPrompt = $"Extract 3 viral 35-second YouTube Shorts scripts from my video \"{stats.TopVideo?.Title ?? ch.ChannelName}\" with a curiosity cliffhanger pointing to the full video."
            });
        }

        var longTitleVideos = videoAudits.Where(v => v.TitleLength > 58).ToList();
        if (longTitleVideos.Count > 0)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-long-titles",
                Severity = "Warning",
                Category = "SEO & Titles",
                Title = $"{longTitleVideos.Count} of {videoAudits.Count} recent titles exceed 58 characters",
                Evidence = $"Average title length is {stats.AvgTitleLength} chars (example: \"{longTitleVideos[0].Title}\"). Mobile YouTube truncates titles past ~55 chars, hiding the payoff hook.",
                ImpactEstimate = "+12% to +25% Browse CTR",
                FixActionType = "metadata-studio",
                FixActionLabel = "Open Quick-Fix Title Studio",
                FixRoute = "",
                FixPrompt = $"Rewrite these {longTitleVideos.Count} truncated titles under 52 characters each for maximum mobile CTR while keeping search keywords first: {string.Join(" | ", longTitleVideos.Take(4).Select(v => v.Title))}"
            });
        }

        if (stats.WeakestVideo is not null && stats.TopVideo is not null && stats.VideosAnalyzed >= 3 && stats.WeakestVideo.ViewRatioVsAvg < 0.6)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-underperforming-video",
                Severity = "Warning",
                Category = "Packaging & CTR",
                Title = $"Recent video lagging at {(stats.WeakestVideo.ViewRatioVsAvg * 100):F0}% of channel average",
                Evidence = $"\"{stats.WeakestVideo.Title}\" has {stats.WeakestVideo.Views:N0} views vs {stats.AvgViewsPerVideo:N0} avg (top video reached {stats.TopVideo.Views:N0} views).",
                ImpactEstimate = "Revive impressions on existing library upload",
                FixActionType = "metadata-studio",
                FixActionLabel = "Fix Weak Video Packaging",
                FixRoute = "",
                FixPrompt = stats.WeakestVideo.FixPrompt
            });
        }

        if (ch.EngagementRatePercentage < config.TargetEngagementPercent)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-low-engagement",
                Severity = "Warning",
                Category = "Engagement & Comments",
                Title = $"Engagement rate ({ch.EngagementRatePercentage:F1}%) trails niche benchmark ({config.TargetEngagementPercent:F1}%)",
                Evidence = $"Viewers are watching without commenting/liking enough to trigger extended Suggested feed loops.",
                ImpactEstimate = "+20% comment velocity & session depth",
                FixActionType = "metadata-studio",
                FixActionLabel = "Copy Pinned Comment CTA",
                FixRoute = "",
                FixPrompt = $"Write 5 high-response pinned comment questions for {ch.ChannelName} ({ch.Niche}) audiences that trigger respectful debate and community replies."
            });
        }

        if (stats.UploadsPerWeek > 0 && stats.UploadsPerWeek < 1.5)
        {
            issues.Add(new ChannelIssueItem
            {
                Id = "issue-cadence",
                Severity = "Opportunity",
                Category = "Upload Consistency",
                Title = $"Publishing cadence (~{stats.UploadsPerWeek:F1}/wk) has room to scale",
                Evidence = $"Recommended rhythm in {ch.Niche} is 2-3 consistent touchpoints per week (mix of core uploads + highlight clips).",
                ImpactEstimate = "+18% to +30% returning viewer momentum",
                FixActionType = "prompt-studio",
                FixActionLabel = "Open 30-Day Content Prompt",
                FixRoute = "",
                FixPrompt = $"Build a realistic 4-week upload calendar (2 uploads/week) for {ch.ChannelName} ({ch.Niche}) inspired by our breakout video \"{stats.TopVideo?.Title ?? ch.ChannelName}\"."
            });
        }

        return issues;
    }

    private static List<AiPromptTemplate> BuildPromptTemplates(
        ChannelMetrics? ch,
        string channelName,
        string handle,
        string niche,
        SocialPlatform platform,
        GrowthConfig config,
        SiteConfig? siteConfig,
        VideoSummaryStats stats,
        List<VideoAuditItem> videoAudits)
    {
        var keywords = config.HighDemandKeywords.Count > 0
            ? string.Join(", ", config.HighDemandKeywords.Take(8))
            : niche;
        var destUrl = !string.IsNullOrWhiteSpace(siteConfig?.DefaultDestinationBaseUrl)
            ? siteConfig.DefaultDestinationBaseUrl
            : "https://keshavsingh.in";
        var topTitles = videoAudits.Take(5).Select(v => $"- \"{v.Title}\" ({v.Views:N0} views)").ToList();
        var recentList = topTitles.Count > 0
            ? string.Join("\n", topTitles)
            : $"- Focus niche: {niche}";

        var ctrPrompt = new StringBuilder()
            .AppendLine($"Act as a top 1% {platform} packaging & CTR strategist for the channel \"{channelName}\" ({handle}) in the \"{niche}\" niche.")
            .AppendLine($"Current stats: {(ch?.FollowersOrSubscribers ?? 0):N0} subscribers, {(ch?.TotalViewsLast30Days ?? 0):N0} views in last 30 days, {stats.AvgTitleLength} avg title characters.")
            .AppendLine("Recent uploads & views:")
            .AppendLine(recentList)
            .AppendLine()
            .AppendLine("Deliver:")
            .AppendLine("1. Rewrite our 3 lowest-view recent titles above into 5 curiosity-driven titles each (STRICTLY under 52 characters so nothing truncates on mobile).")
            .AppendLine("2. For each title, describe a 3-word max thumbnail text overlay + high-contrast visual scene.")
            .AppendLine($"3. Identify the single winning topic pattern from \"{(stats.TopVideo?.Title ?? niche)}\" to repeat twice next month.")
            .ToString().Trim();

        var descPrompt = new StringBuilder()
            .AppendLine($"Act as a YouTube SEO & audience funnel copywriter for \"{channelName}\" ({handle}, Niche: {niche}).")
            .AppendLine($"Target keywords to weave naturally into the first 2 lines: {keywords}")
            .AppendLine($"Destination URL we want viewers to visit: {destUrl}")
            .AppendLine()
            .AppendLine("Deliver:")
            .AppendLine("1. A reusable 120-word YouTube Description template with a benefit-first Line 1 CTA link and SEO keyword paragraph.")
            .AppendLine("2. Three short Pinned Comment options (1 question + 1 resource link CTA + 1 subscribe prompt).")
            .AppendLine("3. 12 comma-separated YouTube video tags and 4 concise hashtags.")
            .ToString().Trim();

        var calendarPrompt = new StringBuilder()
            .AppendLine($"Act as a senior growth producer for \"{channelName}\" ({handle}, {niche} on {platform}).")
            .AppendLine($"Current cadence: ~{(stats.UploadsPerWeek > 0 ? stats.UploadsPerWeek : 1.5):F1} uploads/wk ({stats.ShortsCount} Shorts, {stats.LongFormCount} Long-form in recent batch). Target engagement: {config.TargetEngagementPercent:F1}%.")
            .AppendLine($"Breakout topic to double down on: \"{(stats.TopVideo?.Title ?? niche)}\".")
            .AppendLine()
            .AppendLine("Create a 30-day (8-upload) Growth Content Plan:")
            .AppendLine("- For each upload give: Format (Short vs 8-min Long-form), Title (<=52 chars), Search Keyword target, and 1-sentence premise.")
            .AppendLine("- Show how every Short acts as a 'Related Video' bridge feeding into a higher-RPM long-form pillar.")
            .ToString().Trim();

        var retentionPrompt = new StringBuilder()
            .AppendLine($"Act as a retention script doctor for \"{channelName}\" ({handle}, {niche}).")
            .AppendLine($"Current estimated avg view duration: {(ch?.AverageViewDurationMinutes ?? 3.5):F1} mins vs target {config.TargetAvgWatchMinutes:F1} mins.")
            .AppendLine($"Base this on my topic \"{stats.WeakestVideo?.Title ?? stats.TopVideo?.Title ?? niche}\".")
            .AppendLine()
            .AppendLine("Write:")
            .AppendLine("1. 3 alternative first-15-second cold open hooks (no 'welcome back guys' fluff — open straight in curiosity/story tension).")
            .AppendLine("2. Two pattern-interrupt transitions to insert at minute 1:30 and minute 3:30.")
            .AppendLine("3. A 10-second end-screen bridge that sends 15%+ viewers to the next playlist video before drop-off.")
            .ToString().Trim();

        return
        [
            new()
            {
                Id = "prompt-ctr",
                Category = "Title & Thumbnail",
                Title = "Title & Thumbnail CTR Overhaul",
                Description = "Fixes mobile title truncation (>55 chars) and re-packages low-CTR uploads using real recent view data.",
                TargetTool = "Gemini / ChatGPT / Claude",
                PromptText = ctrPrompt
            },
            new()
            {
                Id = "prompt-seo-funnel",
                Category = "Description & Funnel CTA",
                Title = "SEO Description & Pinned Comment Generator",
                Description = "Turns viewers into traffic for your ecosystem site and lifts comment engagement.",
                TargetTool = "Gemini / ChatGPT / YouTube Studio",
                PromptText = descPrompt
            },
            new()
            {
                Id = "prompt-calendar",
                Category = "30-Day Content Calendar",
                Title = "Shorts-to-Long-Form 30-Day Growth Roadmap",
                Description = "Builds an 8-upload calendar balancing viral Shorts reach with high-RPM long-form watch time.",
                TargetTool = "Gemini / ChatGPT / Claude",
                PromptText = calendarPrompt
            },
            new()
            {
                Id = "prompt-retention",
                Category = "Retention & Hook Script",
                Title = "First 30-Second Hook & Pattern Interrupt Fix",
                Description = "Eliminates early viewer drop-off and lifts average view duration toward niche targets.",
                TargetTool = "Gemini / ChatGPT / Claude",
                PromptText = retentionPrompt
            }
        ];
    }

    private static string BuildRecommendedDescriptionTemplate(
        string channelName,
        string handle,
        string niche,
        GrowthConfig config,
        SiteConfig? siteConfig)
    {
        var header = !string.IsNullOrWhiteSpace(siteConfig?.YouTubeSnippetHeader)
            ? siteConfig.YouTubeSnippetHeader
            : "Explore Tools & Resources:";
        var dest = !string.IsNullOrWhiteSpace(siteConfig?.DefaultDestinationBaseUrl)
            ? siteConfig.DefaultDestinationBaseUrl
            : "https://keshavsingh.in";
        var footer = !string.IsNullOrWhiteSpace(siteConfig?.YouTubeSnippetFooter)
            ? siteConfig.YouTubeSnippetFooter
            : $"Subscribe to {channelName} ({handle}) for regular updates!";
        var hashtags = !string.IsNullOrWhiteSpace(siteConfig?.Hashtags)
            ? siteConfig.Hashtags
            : $"#{Regex.Replace(niche, @"[^A-Za-z0-9]", "")} #{Regex.Replace(channelName, @"[^A-Za-z0-9]", "")}";
        var kw = config.HighDemandKeywords.Count > 0
            ? string.Join(" | ", config.HighDemandKeywords.Take(5))
            : niche;

        return $"{header}\n👉 Visit & Explore: {dest}\n\nAbout this video ({niche}):\nWatch till the end for key insights from {channelName} ({handle}).\nTopics: {kw}\n\n{footer}\n{hashtags}".Trim();
    }

    private static string BuildRecommendedPinnedComment(string channelName, string niche, SiteConfig? siteConfig)
    {
        var dest = !string.IsNullOrWhiteSpace(siteConfig?.DefaultDestinationBaseUrl)
            ? siteConfig.DefaultDestinationBaseUrl
            : "https://keshavsingh.in";
        return $"🙏 Thanks for watching {channelName}! Which part of this {niche} video helped or inspired you most? Comment below! 👇\n🔗 Explore more tools & daily updates: {dest}";
    }

    private static string BuildExecutiveSummary(
        ChannelMetrics? ch,
        string channelName,
        SocialPlatform platform,
        double healthScore,
        VideoSummaryStats stats,
        List<ChannelIssueItem> issues)
    {
        if (ch is null)
            return $"Showing benchmark {platform} growth plan. Analyze any channel live on the Dashboard or load a handle above for per-video diagnostics and tailored AI prompts.";

        var topIssue = issues.FirstOrDefault()?.Title ?? "Maintain consistent weekly upload schedule";
        if (stats.TopVideo is not null)
        {
            return $"{channelName} scores {healthScore:F1}/100 ({ComputeGrade(healthScore)}) across {stats.VideosAnalyzed} recent videos (avg {stats.AvgViewsPerVideo:N0} views; breakout \"{stats.TopVideo.Title}\" hit {stats.TopVideo.Views:N0} views). Top focus: {topIssue}.";
        }
        return $"{channelName} scores {healthScore:F1}/100 ({ComputeGrade(healthScore)}) with {ch.TotalViewsLast30Days:N0} 30-day views. Top priority: {topIssue}.";
    }

    private static List<string> BuildUploadTimes(SocialPlatform platform, GrowthConfig config)
    {
        var uploadTimes = new List<string>();
        var platformKey = platform.ToString();
        if (config.BestUploadTimes.TryGetValue(platformKey, out var primaryTime))
            uploadTimes.Add($"{platformKey} Peak: {primaryTime}");

        foreach (var kv in config.BestUploadTimes)
        {
            if (!kv.Key.Equals(platformKey, StringComparison.OrdinalIgnoreCase))
                uploadTimes.Add($"{kv.Key}: {kv.Value}");
        }
        return uploadTimes;
    }

    private static string ComputeGrade(double score) => score switch
    {
        >= 90 => "A+",
        >= 82 => "A",
        >= 74 => "B+",
        >= 64 => "B",
        >= 52 => "C",
        _ => "D"
    };

    private static bool ContainsHookSignal(string title) =>
        title.Any(char.IsDigit) || title.Contains('?') || title.Contains('!') || title.Contains('|') || title.Contains(':');

    private static string BuildVideoDiagnosticNote(string title, double ratio, bool isShort, bool hasLink, bool hasHook, string tag)
    {
        var notes = new List<string>();
        if (tag == "Breakout") notes.Add($"Breakout {(ratio * 100):F0}% of avg");
        else if (tag == "Underperforming") notes.Add($"Below avg ({(ratio * 100):F0}%) — test new hook");

        if (title.Length > 58) notes.Add($"{title.Length} chars (truncates on mobile)");
        else if (title.Length < 18 && !isShort) notes.Add("Title too short for search SEO");

        if (!hasLink) notes.Add("No funnel link in snippet");
        if (!hasHook) notes.Add("Weak curiosity/number signal");

        return notes.Count > 0 ? string.Join(" • ", notes) : (isShort ? "Clean Short packaging" : "Healthy packaging & metadata");
    }

    private static string BuildSuggestedTitleRewrite(string original, string channelName, string niche, bool isShort)
    {
        var clean = Regex.Replace(original, @"#\S+", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
            clean = $"{niche} Highlights";

        var core = clean.Length > 38 ? clean[..38].TrimEnd(' ', '-', '|', ':') : clean;
        return isShort ? $"{core} | Watch Till End #shorts" : $"{core} — Full {niche} Guide";
    }

    private static string BuildSingleVideoFixPrompt(
        string title,
        long views,
        double avgViews,
        string niche,
        string channelName,
        bool isShort,
        bool hasLink,
        string suggestedTitle)
    {
        var format = isShort ? "YouTube Short" : "Long-form YouTube Video";
        return $"Optimize this {format} on my channel \"{channelName}\" ({niche}):\n" +
               $"- Current Title: \"{title}\"\n" +
               $"- Performance: {views:N0} views (channel recent avg: {avgViews:N0})\n" +
               $"- Funnel Link Present: {(hasLink ? "Yes" : "No — missing external link")}\n" +
               $"- Draft Idea: \"{suggestedTitle}\"\n\n" +
               "Give me: (1) 5 clickable titles under 52 chars, (2) a bold 3-word thumbnail text concept, (3) an improved opening 2-line description with CTA link, and (4) 1 pinned comment to boost replies.";
    }

    private static List<GrowthSuggestion> BuildChannelSpecificSuggestions(
        ChannelMetrics? ch,
        SocialPlatform platform,
        GrowthConfig config)
    {
        if (ch is null) return [];
        var list = new List<GrowthSuggestion>();

        if (ch.EngagementRatePercentage < config.TargetEngagementPercent)
        {
            list.Add(new GrowthSuggestion
            {
                Id = "auto-eng",
                Category = "Engagement Velocity",
                Title = $"Lift {ch.ChannelName} engagement from {ch.EngagementRatePercentage}% to {config.TargetEngagementPercent}%+",
                Description = "Current like/comment rate per view trails top algorithmic distribution thresholds for this category.",
                Impact = SuggestionImpact.High,
                ActionItem = "Pin a binary opinion question in the top comment and prompt viewers at 60% watch completion.",
                EstimatedGain = "+22% Comment & Like Ratio",
                ApplicablePlatform = platform
            });
        }

        if (ch.FollowersOrSubscribers > 0 && ch.TotalViewsLast30Days > 0)
        {
            var ratio = (double)ch.TotalViewsLast30Days / ch.FollowersOrSubscribers;
            if (ratio < 0.25)
            {
                list.Add(new GrowthSuggestion
                {
                    Id = "auto-browse",
                    Category = "Browse & Thumbnail CTR",
                    Title = "Re-activate existing subscribers & non-subscriber Browse traffic",
                    Description = $"Monthly views ({ch.TotalViewsLast30Days:N0}) are {(ratio * 100):F0}% of total audience ({ch.FollowersOrSubscribers:N0}).",
                    Impact = SuggestionImpact.High,
                    ActionItem = "A/B test shorter punchier 45-character titles and high-contrast 3-word thumbnail hooks on the next 3 uploads.",
                    EstimatedGain = "+35% Browse & Suggested Traffic",
                    ApplicablePlatform = platform
                });
            }
        }

        if (ch.RecentVideos.Count >= 3)
        {
            var recentSpanDays = Math.Max(1.0, (ch.RecentVideos.Max(v => v.PublishedAt) - ch.RecentVideos.Min(v => v.PublishedAt)).TotalDays);
            var perWeek = ch.RecentVideos.Count / (recentSpanDays / 7.0);
            if (perWeek < 1.2)
            {
                list.Add(new GrowthSuggestion
                {
                    Id = "auto-cadence",
                    Category = "Publishing Cadence",
                    Title = $"Increase upload consistency (currently ~{perWeek:F1} videos/week)",
                    Description = "Consistent weekly slots train subscriber expectations and maintain home-feed session momentum.",
                    Impact = SuggestionImpact.Medium,
                    ActionItem = "Add 1 companion Short remixing highlights from your best-performing recent upload each week.",
                    EstimatedGain = "+18-30% Returning Viewers",
                    ApplicablePlatform = platform
                });
            }
        }

        return list;
    }
}
