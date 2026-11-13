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

    public async Task<List<ChannelMetrics>> GetChannelsForUserAsync(
        string? callerUserId,
        bool isAdmin,
        bool includeInactive = false)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return [];

        var channels = await _store.GetListAsync(Collection, SocialSeedData.DefaultChannels);
        return channels
            .Where(c => (includeInactive || c.IsActive) && IsOwnedByCaller(c, callerUserId, isAdmin))
            .OrderByDescending(c => c.TotalViewsLast30Days)
            .ToList();
    }

    public static bool IsOwnedByCaller(ChannelMetrics c, string? callerUserId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return false;

        if (isAdmin)
            return string.Equals(c.OwnerUserId, callerUserId, StringComparison.Ordinal) ||
                   c.IsAdminOwned ||
                   string.IsNullOrWhiteSpace(c.OwnerUserId);

        return !c.IsAdminOwned && string.Equals(c.OwnerUserId, callerUserId, StringComparison.Ordinal);
    }

    public async Task<ChannelMetrics?> GetOwnedChannelAsync(string callerUserId, bool isAdmin, string id)
    {
        if (string.IsNullOrWhiteSpace(callerUserId) || string.IsNullOrWhiteSpace(id))
            return null;

        var channels = await _store.GetListAsync(Collection, SocialSeedData.DefaultChannels);
        return channels.FirstOrDefault(c =>
            c.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
            IsOwnedByCaller(c, callerUserId, isAdmin));
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

    public async Task<ChannelMetrics?> UpsertUserChannelAsync(
        string callerUserId,
        bool isAdmin,
        ChannelMetrics channel)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return null;

        if (!string.IsNullOrWhiteSpace(channel.Id))
        {
            var all = await _store.GetListAsync(Collection, SocialSeedData.DefaultChannels);
            var existing = all.FirstOrDefault(c => c.Id.Equals(channel.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && !IsOwnedByCaller(existing, callerUserId, isAdmin))
                return null;
        }
        else
        {
            // Prevent duplicate per-user handle entries on repeated Save clicks.
            var existingMine = await GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: true);
            var dup = existingMine.FirstOrDefault(c =>
                (!string.IsNullOrWhiteSpace(channel.ExternalChannelId) &&
                 c.ExternalChannelId.Equals(channel.ExternalChannelId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(channel.ChannelHandle) &&
                 c.Platform == channel.Platform &&
                 c.ChannelHandle.Equals(channel.ChannelHandle, StringComparison.OrdinalIgnoreCase)));

            channel.Id = dup?.Id ?? $"uch-{Guid.NewGuid():N}"[..12];
        }

        channel.OwnerUserId = callerUserId;
        channel.IsAdminOwned = isAdmin;
        return await UpsertChannelAsync(channel);
    }

    public async Task<bool> DeleteUserChannelAsync(string callerUserId, bool isAdmin, string id)
    {
        var owned = await GetOwnedChannelAsync(callerUserId, isAdmin, id);
        if (owned is null) return false;
        return await DeleteChannelAsync(owned.Id);
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
        return ProjectRecentVideos(channels, limit);
    }

    public async Task<List<RecentFeedVideo>> GetRecentVideosForUserAsync(
        string? callerUserId,
        bool isAdmin,
        int limit = 24)
    {
        var channels = await GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: false);
        return ProjectRecentVideos(channels, limit);
    }

    private static List<RecentFeedVideo> ProjectRecentVideos(List<ChannelMetrics> channels, int limit) =>
        channels
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

    public async Task<List<PlatformSummary>> GetPlatformSummariesAsync()
    {
        var channels = await GetAllChannelsAsync(includeInactive: false);
        return await BuildPlatformSummariesAsync(channels);
    }

    public async Task<List<PlatformSummary>> GetPlatformSummariesForUserAsync(
        string? callerUserId,
        bool isAdmin)
    {
        var channels = await GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: false);
        return await BuildPlatformSummariesAsync(channels);
    }

    private async Task<List<PlatformSummary>> BuildPlatformSummariesAsync(List<ChannelMetrics> channels)
    {
        var earningsConfig = await _configService.GetEarningsConfigAsync();
        var benchmarkMap = earningsConfig.PlatformBenchmarks
            .ToDictionary(b => b.Platform, b => b.DefaultRpmUsd, StringComparer.OrdinalIgnoreCase);

        return channels
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
    }
}
