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

    public async Task<OutlierBenchmarkReport> GetOutliersForUserAsync(
        string? callerUserId,
        bool isAdmin,
        IEnumerable<ChannelMetrics>? extraChannels = null,
        int limit = 20)
    {
        var owned = await GetChannelsForUserAsync(callerUserId, isAdmin, includeInactive: false);
        var combined = MergeDistinctChannels(owned, extraChannels);
        return BuildOutlierBenchmarks(combined, limit);
    }

    public static OutlierBenchmarkReport BuildOutlierBenchmarks(
        IEnumerable<ChannelMetrics> channels,
        int limit = 20,
        DateTime? nowUtcOverride = null)
    {
        var nowUtc = nowUtcOverride?.ToUniversalTime() ?? DateTime.UtcNow;
        var channelList = (channels ?? [])
            .Where(c => c is not null && c.IsActive)
            .ToList();

        var channelLeaderboard = new List<ChannelGrowthBenchmark>();
        var allVideoBenchmarks = new List<OutlierVideoBenchmark>();

        foreach (var ch in channelList)
        {
            var videos = (ch.RecentVideos ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.VideoId) || !string.IsNullOrWhiteSpace(v.Title))
                .ToList();

            var orderedViews = videos.Select(v => v.Views).OrderBy(v => v).ToList();
            var baselineViews = orderedViews.Count == 0
                ? Math.Max(1.0, ch.TotalViewsLast30Days / 10.0)
                : Math.Max(1.0, Median(orderedViews));

            var channelVideos = new List<OutlierVideoBenchmark>(videos.Count);
            foreach (var v in videos)
            {
                var publishedUtc = v.PublishedAt == default
                    ? nowUtc.AddDays(-1)
                    : v.PublishedAt.ToUniversalTime();
                var ageDays = Math.Max(1.0, (nowUtc - publishedUtc).TotalDays);
                var viewsPerSub = ch.FollowersOrSubscribers > 0
                    ? Math.Round(v.Views / (double)ch.FollowersOrSubscribers, 2)
                    : 0.0;
                var viewsPerDay = Math.Round(v.Views / ageDays, 1);
                var breakout = Math.Round(v.Views / baselineViews, 2);
                var isShort = v.Url.Contains("/shorts/", StringComparison.OrdinalIgnoreCase) ||
                              v.Title.Contains("#short", StringComparison.OrdinalIgnoreCase);

                var tier = (ch.FollowersOrSubscribers > 0 && viewsPerSub >= 3.0) || breakout >= 3.0
                    ? "Viral Outlier"
                    : (ch.FollowersOrSubscribers > 0 && viewsPerSub >= 1.5) || breakout >= 1.5
                        ? "Strong Breakout"
                        : "Steady";

                var item = new OutlierVideoBenchmark
                {
                    ChannelId = ch.Id,
                    ChannelName = ch.ChannelName,
                    ChannelHandle = ch.ChannelHandle,
                    Platform = ch.Platform,
                    ChannelSubscribers = ch.FollowersOrSubscribers,
                    VideoId = v.VideoId,
                    Title = v.Title,
                    Url = v.Url,
                    ThumbnailUrl = v.ThumbnailUrl,
                    PublishedAt = publishedUtc,
                    Views = v.Views,
                    ViewsPerSubscriber = viewsPerSub,
                    ViewsPerDay = viewsPerDay,
                    BreakoutMultiplier = breakout,
                    IsShort = isShort,
                    OutlierTier = tier
                };
                channelVideos.Add(item);
                allVideoBenchmarks.Add(item);
            }

            var topChannelVideo = channelVideos
                .OrderByDescending(v => v.ViewsPerSubscriber)
                .ThenByDescending(v => v.BreakoutMultiplier)
                .ThenByDescending(v => v.ViewsPerDay)
                .FirstOrDefault();

            var channelViewsPerSub = ch.FollowersOrSubscribers > 0
                ? Math.Round(ch.TotalViewsLast30Days / (double)ch.FollowersOrSubscribers, 2)
                : 0.0;
            var avgVideoOutlier = channelVideos.Count > 0
                ? Math.Round(channelVideos.Average(v => v.ViewsPerSubscriber), 2)
                : channelViewsPerSub;
            var avgViewsPerDay = channelVideos.Count > 0
                ? Math.Round(channelVideos.Average(v => v.ViewsPerDay), 1)
                : Math.Round(ch.TotalViewsLast30Days / 30.0, 1);
            var outlierCount = channelVideos.Count(v => v.BreakoutMultiplier >= 1.5 || v.ViewsPerSubscriber >= 1.5);

            var velocityScore = Math.Round(
                Math.Max(0.0, ch.GrowthRateMonthlyPercentage) * 1.5 +
                avgVideoOutlier * 12.0 +
                Math.Log10(Math.Max(1.0, avgViewsPerDay)) * 8.0 +
                outlierCount * 4.0,
                1);

            channelLeaderboard.Add(new ChannelGrowthBenchmark
            {
                ChannelId = ch.Id,
                ChannelName = ch.ChannelName,
                ChannelHandle = ch.ChannelHandle,
                Platform = ch.Platform,
                Niche = ch.Niche,
                FollowersOrSubscribers = ch.FollowersOrSubscribers,
                TotalViewsLast30Days = ch.TotalViewsLast30Days,
                GrowthRateMonthlyPercentage = ch.GrowthRateMonthlyPercentage,
                ViewsPerSubscriber = channelViewsPerSub,
                AvgVideoOutlierScore = avgVideoOutlier,
                AvgViewsPerDay = avgViewsPerDay,
                OutlierVideoCount = outlierCount,
                GrowthVelocityScore = velocityScore,
                TopOutlierVideo = topChannelVideo
            });
        }

        var sortedChannels = channelLeaderboard
            .OrderByDescending(c => c.GrowthVelocityScore)
            .ThenByDescending(c => c.GrowthRateMonthlyPercentage)
            .ThenByDescending(c => c.ViewsPerSubscriber)
            .ToList();

        var sortedVideos = allVideoBenchmarks
            .OrderByDescending(v => v.ViewsPerSubscriber)
            .ThenByDescending(v => v.BreakoutMultiplier)
            .ThenByDescending(v => v.ViewsPerDay)
            .Take(Math.Clamp(limit, 1, 60))
            .ToList();

        var positiveVps = allVideoBenchmarks
            .Select(v => v.ViewsPerSubscriber)
            .Where(x => x > 0)
            .OrderBy(x => x)
            .ToList();

        return new OutlierBenchmarkReport
        {
            GeneratedAtUtc = nowUtc,
            ChannelsEvaluated = sortedChannels.Count,
            VideosEvaluated = allVideoBenchmarks.Count,
            MedianViewsPerSubscriber = positiveVps.Count == 0 ? 0.0 : Math.Round(Median(positiveVps), 2),
            TopOutlierVideo = sortedVideos.FirstOrDefault(),
            FastestGrowingChannel = sortedChannels.FirstOrDefault(),
            ChannelLeaderboard = sortedChannels,
            TopOutlierVideos = sortedVideos
        };
    }

    private static List<ChannelMetrics> MergeDistinctChannels(
        IEnumerable<ChannelMetrics> primary,
        IEnumerable<ChannelMetrics>? extras)
    {
        var list = primary.ToList();
        if (extras is null) return list;

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ch in list)
        {
            if (!string.IsNullOrWhiteSpace(ch.ExternalChannelId)) seenKeys.Add($"ext:{ch.ExternalChannelId}");
            if (!string.IsNullOrWhiteSpace(ch.ChannelHandle)) seenKeys.Add($"hdl:{ch.ChannelHandle.Trim()}");
        }

        foreach (var ex in extras.Where(c => c is not null))
        {
            var extKey = !string.IsNullOrWhiteSpace(ex.ExternalChannelId) ? $"ext:{ex.ExternalChannelId}" : null;
            var hdlKey = !string.IsNullOrWhiteSpace(ex.ChannelHandle) ? $"hdl:{ex.ChannelHandle.Trim()}" : null;
            if ((extKey is not null && seenKeys.Contains(extKey)) || (hdlKey is not null && seenKeys.Contains(hdlKey)))
                continue;

            if (extKey is not null) seenKeys.Add(extKey);
            if (hdlKey is not null) seenKeys.Add(hdlKey);
            list.Add(ex);
        }

        return list;
    }

    private static double Median(IReadOnlyList<long> sorted)
    {
        var n = sorted.Count;
        if (n == 0) return 0;
        return (n & 1) == 1 ? sorted[n / 2] : (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        var n = sorted.Count;
        if (n == 0) return 0;
        return (n & 1) == 1 ? sorted[n / 2] : (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
    }
}
