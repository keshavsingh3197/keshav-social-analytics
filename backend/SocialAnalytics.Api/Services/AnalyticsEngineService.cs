using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class AnalyticsEngineService
{
    private const string Collection = "channels";
    private readonly HybridDataStore _store;
    private readonly SocialConfigService _configService;

    public AnalyticsEngineService(HybridDataStore store, SocialConfigService configService)
    {
        _store = store;
        _configService = configService;
    }

    public async Task<List<ChannelMetrics>> GetAllChannelsAsync(bool includeInactive = false)
    {
        var channels = await _store.GetListAsync(Collection, SocialSeedData.DefaultChannels);
        return (includeInactive ? channels : channels.Where(c => c.IsActive))
            .OrderByDescending(c => c.TotalViewsLast30Days)
            .ToList();
    }

    public async Task<ChannelMetrics> UpsertChannelAsync(ChannelMetrics channel)
    {
        if (string.IsNullOrWhiteSpace(channel.Id))
            channel.Id = $"ch-{Guid.NewGuid():N}"[..10];

        if (channel.EstimatedMonthlyRevenue <= 0 && channel.TotalViewsLast30Days > 0 && channel.RpmUsd > 0)
            channel.EstimatedMonthlyRevenue = Math.Round((channel.TotalViewsLast30Days / 1000.0) * channel.RpmUsd, 2);

        channel.LastUpdated = DateTime.UtcNow;
        return await _store.UpsertItemAsync(Collection, channel.Id, c => c.Id, channel, SocialSeedData.DefaultChannels);
    }

    public Task<bool> DeleteChannelAsync(string id) =>
        _store.DeleteItemAsync(Collection, id, c => c.Id, SocialSeedData.DefaultChannels);

    public async Task<int> PurgeLegacyDemoChannelsAsync()
    {
        var channels = await _store.GetListAsync(Collection, SocialSeedData.DefaultChannels);
        var remaining = channels
            .Where(c =>
                !SocialSeedData.LegacyDemoChannelIds.Contains(c.Id) &&
                !SocialSeedData.LegacyDemoHandles.Contains(c.ChannelHandle))
            .ToList();

        var removedCount = channels.Count - remaining.Count;
        if (removedCount > 0)
        {
            await _store.SaveListAsync(Collection, remaining);
        }
        return removedCount;
    }

    public async Task<List<RecentFeedVideo>> GetRecentVideosAsync(int limit = 24)
    {
        var channels = await GetAllChannelsAsync(includeInactive: false);
        return channels
            .SelectMany(ch => (ch.RecentVideos ?? []).Select(v => new RecentFeedVideo
            {
                ChannelId = ch.Id,
                ChannelName = ch.ChannelName,
                ChannelHandle = ch.ChannelHandle,
                VideoId = v.VideoId,
                Title = v.Title,
                Url = v.Url,
                ThumbnailUrl = v.ThumbnailUrl,
                PublishedAt = v.PublishedAt,
                Views = v.Views,
                DescriptionSnippet = v.DescriptionSnippet
            }))
            .OrderByDescending(v => v.PublishedAt)
            .Take(Math.Clamp(limit, 1, 60))
            .ToList();
    }

    public async Task<List<PlatformSummary>> GetPlatformSummariesAsync()
    {
        var channels = await GetAllChannelsAsync(includeInactive: false);
        var earningsConfig = await _configService.GetEarningsConfigAsync();
        var benchmarkMap = earningsConfig.PlatformBenchmarks
            .ToDictionary(b => b.Platform, b => b.DefaultRpmUsd, StringComparer.OrdinalIgnoreCase);

        var summaries = channels
            .GroupBy(c => c.Platform)
            .Select(group =>
            {
                var totalViews = group.Sum(c => c.TotalViewsLast30Days);
                var totalAudience = group.Sum(c => c.FollowersOrSubscribers);
                var avgEngagement = group.Any()
                    ? Math.Round(group.Average(c => c.EngagementRatePercentage), 1)
                    : 0;

                decimal totalEarnings = 0m;
                foreach (var ch in group)
                {
                    if (ch.EstimatedMonthlyRevenue > 0)
                    {
                        totalEarnings += (decimal)ch.EstimatedMonthlyRevenue;
                    }
                    else
                    {
                        var rpm = ch.RpmUsd > 0
                            ? ch.RpmUsd
                            : benchmarkMap.TryGetValue(ch.Platform.ToString(), out var fallback) ? fallback : 2.5;
                        totalEarnings += (ch.TotalViewsLast30Days / 1000m) * (decimal)rpm;
                    }
                }

                return new PlatformSummary
                {
                    Platform = group.Key,
                    TotalAudience = totalAudience,
                    TotalViews = totalViews,
                    AvgEngagement = avgEngagement,
                    EstimatedEarningsMonthly = Math.Round(totalEarnings, 2),
                    ChannelCount = group.Count()
                };
            })
            .OrderByDescending(s => s.EstimatedEarningsMonthly)
            .ToList();

        return summaries;
    }
}
