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

public class RecentVideoItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public long Views { get; set; }
    public string DescriptionSnippet { get; set; } = string.Empty;
}

public class RecentFeedVideo
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public long Views { get; set; }
    public string DescriptionSnippet { get; set; } = string.Empty;
}

public class ChannelMetrics
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; }
    public string Niche { get; set; } = "General";
    public string ExternalChannelId { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Admin";
    public bool IsVerifiedLive { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public long FollowersOrSubscribers { get; set; }
    public long TotalViewsLast30Days { get; set; }
    public double EngagementRatePercentage { get; set; }
    public double AverageViewDurationMinutes { get; set; }
    public int VideosOrPostsCount { get; set; }
    public double GrowthRateMonthlyPercentage { get; set; }
    public double RpmUsd { get; set; } = 3.5;
    public double EstimatedMonthlyRevenue { get; set; }
    public string ChannelUrl { get; set; } = string.Empty;
    public List<RecentVideoItem> RecentVideos { get; set; } = [];
    public string OwnerUserId { get; set; } = string.Empty;
    public bool IsAdminOwned { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class ChannelSearchCandidate
{
    public string ExternalChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; } = SocialPlatform.YouTube;
    public string AvatarUrl { get; set; } = string.Empty;
    public long FollowersOrSubscribers { get; set; }
    public int VideoCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Public";
    public bool IsVerified { get; set; }
}

public class PublicChannelLookupRequest
{
    public string Query { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; } = SocialPlatform.YouTube;
    public string? Niche { get; set; }
}

public class PublicChannelAnalysisResult
{
    public ChannelMetrics Channel { get; set; } = new();
    public EarningsReport Earnings { get; set; } = new();
    public ChannelAuditResult Audit { get; set; } = new();
    public List<string> AutoInsights { get; set; } = [];
    public bool ResolvedLive { get; set; }
    public string ResolutionSummary { get; set; } = string.Empty;
}

public class PlatformSummary
{
    public SocialPlatform Platform { get; set; }
    public long TotalAudience { get; set; }
    public long TotalViews { get; set; }
    public double AvgEngagement { get; set; }
    public decimal EstimatedEarningsMonthly { get; set; }
    public int ChannelCount { get; set; }
}
