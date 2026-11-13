namespace SocialAnalytics.Api.Models;

public enum SuggestionImpact
{
    High,
    Medium,
    Low
}

public class GrowthSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Category { get; set; } = string.Empty; // "SEO & Title", "Retention", "Upload Timing", "Thumbnail", "Monetization"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SuggestionImpact Impact { get; set; }
    public string ActionItem { get; set; } = string.Empty;
    public string EstimatedGain { get; set; } = string.Empty; // e.g. "+15% CTR", "+30% Watch Time"
    public SocialPlatform ApplicablePlatform { get; set; }
}

public class AuditScorePillar
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Status { get; set; } = "Solid"; // "Strong", "Needs Work", "Critical"
    public string Summary { get; set; } = string.Empty;
}

public class VideoAuditItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public long Views { get; set; }
    public double ViewRatioVsAvg { get; set; }
    public int TitleLength { get; set; }
    public bool IsShort { get; set; }
    public bool HasDescriptionLink { get; set; }
    public bool HasHookOrStructure { get; set; }
    public string PerformanceTag { get; set; } = "Solid"; // "Breakout", "Solid", "Underperforming"
    public string DiagnosticNote { get; set; } = string.Empty;
    public string SuggestedTitleRewrite { get; set; } = string.Empty;
    public string FixPrompt { get; set; } = string.Empty;
}

public class ChannelIssueItem
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // "Critical", "Warning", "Opportunity"
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string ImpactEstimate { get; set; } = string.Empty;
    public string FixActionType { get; set; } = "metadata-studio"; // "metadata-studio", "funnel-builder", "refresh-live", "save-workspace", "connect-youtube", "prompt-studio"
    public string FixActionLabel { get; set; } = "Fix This Now";
    public string FixRoute { get; set; } = string.Empty;
    public string FixPrompt { get; set; } = string.Empty;
}

public class AiPromptTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetTool { get; set; } = "Gemini / ChatGPT / Claude";
    public string PromptText { get; set; } = string.Empty;
}

public class ChannelAuditSnapshot
{
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public string Platform { get; set; } = "YouTube";
    public string Niche { get; set; } = "General";
    public string AvatarUrl { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Public";
    public bool IsVerifiedLive { get; set; }
    public long FollowersOrSubscribers { get; set; }
    public long TotalViewsLast30Days { get; set; }
    public double EngagementRatePercentage { get; set; }
    public double AverageViewDurationMinutes { get; set; }
    public int VideosAnalyzed { get; set; }
    public int ShortsCount { get; set; }
    public int LongFormCount { get; set; }
    public long AvgViewsPerRecentVideo { get; set; }
    public double UploadsPerWeek { get; set; }
    public int AvgTitleLength { get; set; }
    public int VideosWithDescriptionLinks { get; set; }
    public double EstimatedRpmUsd { get; set; }
    public double EstimatedMonthlyRevenue { get; set; }
}

public class ChannelAuditResult
{
    public string ChannelHandle { get; set; } = string.Empty;
    public double HealthScoreOutOf100 { get; set; }
    public string OverallGrade { get; set; } = "B";
    public string ExecutiveSummary { get; set; } = string.Empty;
    public ChannelAuditSnapshot? Snapshot { get; set; }
    public List<AuditScorePillar> ScorePillars { get; set; } = new();
    public List<ChannelIssueItem> DetectedIssues { get; set; } = new();
    public List<VideoAuditItem> VideoAudits { get; set; } = new();
    public List<AiPromptTemplate> PromptTemplates { get; set; } = new();
    public string RecommendedDescriptionTemplate { get; set; } = string.Empty;
    public string RecommendedPinnedComment { get; set; } = string.Empty;
    public List<GrowthSuggestion> PrioritySuggestions { get; set; } = new();
    public List<string> BestUploadTimes { get; set; } = new();
    public List<string> HighDemandKeywords { get; set; } = new();
}
