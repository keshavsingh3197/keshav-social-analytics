using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class GrowthAdvisorService
{
    private const string SuggestionsCollection = "growth_suggestions";
    private const string ConfigCollection = "growth_config";

    private readonly HybridDataStore _store;
    private readonly AnalyticsEngineService _analyticsService;

    public GrowthAdvisorService(HybridDataStore store, AnalyticsEngineService analyticsService)
    {
        _store = store;
        _analyticsService = analyticsService;
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

        ChannelMetrics? matchedChannel = transientChannel;
        if (matchedChannel is null && !string.IsNullOrWhiteSpace(callerUserId))
        {
            var userChannels = await _analyticsService.GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: true);
            matchedChannel = userChannels.FirstOrDefault(c =>
                c.ChannelHandle.Equals(channelHandle, StringComparison.OrdinalIgnoreCase) ||
                c.ChannelName.Equals(channelHandle, StringComparison.OrdinalIgnoreCase));
        }

        return BuildAuditResult(matchedChannel, channelHandle, platform, config, allSuggestions);
    }

    public static ChannelAuditResult BuildAuditResult(
        ChannelMetrics? matchedChannel,
        string channelHandle,
        SocialPlatform platform,
        GrowthConfig config,
        List<GrowthSuggestion> allSuggestions)
    {
        var dynamicSuggestions = BuildChannelSpecificSuggestions(matchedChannel, platform, config);

        // Order tailored diagnostics + platform-matched suggestions first, then others sorted by impact.
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
            healthScore = Math.Round(Math.Clamp(engScore + durationScore + growthScore + volumeScore, 42.0, 99.2), 1);
        }

        var uploadTimes = new List<string>();
        var platformKey = platform.ToString();
        if (config.BestUploadTimes.TryGetValue(platformKey, out var primaryTime))
            uploadTimes.Add($"{platformKey} Peak: {primaryTime}");

        foreach (var kv in config.BestUploadTimes)
        {
            if (!kv.Key.Equals(platformKey, StringComparison.OrdinalIgnoreCase))
                uploadTimes.Add($"{kv.Key}: {kv.Value}");
        }

        return new ChannelAuditResult
        {
            ChannelHandle = string.IsNullOrWhiteSpace(matchedChannel?.ChannelHandle) ? channelHandle : matchedChannel.ChannelHandle,
            HealthScoreOutOf100 = healthScore,
            PrioritySuggestions = prioritySuggestions,
            BestUploadTimes = uploadTimes,
            HighDemandKeywords = config.HighDemandKeywords
        };
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
