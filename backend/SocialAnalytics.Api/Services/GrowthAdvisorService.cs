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

    public async Task<ChannelAuditResult> AuditChannelAsync(string channelHandle, SocialPlatform platform)
    {
        var allSuggestions = await GetAllSuggestionsAsync();
        var config = await GetGrowthConfigAsync();
        var channels = await _analyticsService.GetAllChannelsAsync(includeInactive: true);

        var matchedChannel = channels.FirstOrDefault(c =>
            c.ChannelHandle.Equals(channelHandle, StringComparison.OrdinalIgnoreCase) ||
            c.ChannelName.Equals(channelHandle, StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault(c => c.Platform == platform);

        // Order platform-matched suggestions first, then others sorted by impact.
        var prioritySuggestions = allSuggestions
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
            })
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
            ChannelHandle = matchedChannel?.ChannelHandle ?? channelHandle,
            HealthScoreOutOf100 = healthScore,
            PrioritySuggestions = prioritySuggestions,
            BestUploadTimes = uploadTimes,
            HighDemandKeywords = config.HighDemandKeywords
        };
    }
}
