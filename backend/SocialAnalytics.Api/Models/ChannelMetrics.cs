namespace SocialAnalytics.Api.Models;

public enum SocialPlatform
{
    YouTube,
    Instagram,
    Facebook,
    TikTok,
    LinkedIn,
    Twitter
}

public class ChannelMetrics
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; }
    public string Niche { get; set; } = "General";
    public long FollowersOrSubscribers { get; set; }
    public long TotalViewsLast30Days { get; set; }
    public double EngagementRatePercentage { get; set; }
    public double AverageViewDurationMinutes { get; set; }
    public int VideosOrPostsCount { get; set; }
    public double GrowthRateMonthlyPercentage { get; set; }
    public double RpmUsd { get; set; } = 3.5;
    public double EstimatedMonthlyRevenue { get; set; }
    public string ChannelUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class PlatformSummary
{
    public SocialPlatform Platform { get; set; }
    public long TotalAudience { get; set; }
    public long TotalViews { get; set; }
    public double AvgEngagement { get; set; }
    public decimal EstimatedEarningsMonthly { get; set; }
}
