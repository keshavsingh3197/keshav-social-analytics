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

public class ChannelAuditResult
{
    public string ChannelHandle { get; set; } = string.Empty;
    public double HealthScoreOutOf100 { get; set; }
    public List<GrowthSuggestion> PrioritySuggestions { get; set; } = new();
    public List<string> BestUploadTimes { get; set; } = new();
    public List<string> HighDemandKeywords { get; set; } = new();
}
