namespace SocialAnalytics.Api.Models;

public class PlatformBenchmark
{
    public string Platform { get; set; } = string.Empty;
    public string Icon { get; set; } = "📊";
    public long DefaultMonthlyViews { get; set; } = 100000;
    public double DefaultRpmUsd { get; set; } = 3.5;
    public double DefaultSponsorshipUsd { get; set; } = 250;
    public double AffiliateConversionRatePercent { get; set; } = 2.0;
    public double AvgOrderValueUsd { get; set; } = 29;
    public string Description { get; set; } = string.Empty;
}

public class NichePreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double MinRpmUsd { get; set; }
    public double MaxRpmUsd { get; set; }
    public double SuggestedRpmUsd { get; set; }
    public string Tip { get; set; } = string.Empty;
}

public class MonetizationOpportunity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Impact { get; set; } = "High";
    public string Description { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
}

public class EarningsConfig
{
    public string Id { get; set; } = "default";
    public List<PlatformBenchmark> PlatformBenchmarks { get; set; } = [];
    public List<NichePreset> Niches { get; set; } = [];
    public List<MonetizationOpportunity> Opportunities { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class GrowthConfig
{
    public string Id { get; set; } = "default";
    public Dictionary<string, string> BestUploadTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> HighDemandKeywords { get; set; } = [];
    public double TargetEngagementPercent { get; set; } = 6.0;
    public double TargetAvgWatchMinutes { get; set; } = 7.5;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SiteConfig
{
    public string Id { get; set; } = "default";
    public string HeroTitle { get; set; } = "Social Channel Performance & Growth Hub";
    public string HeroSubtitle { get; set; } = "Track multi-platform engagement across YouTube, Instagram, and Facebook. Maximize audience retention, calculate creator earnings, and funnel viewer traffic directly into your monetized ecosystems.";
    public string YouTubeSnippetHeader { get; set; } = "🚀 Explore Keshav Singh Ecosystem Tools:";
    public string YouTubeSnippetFooter { get; set; } = "🔔 Subscribe for weekly AI, engineering & cultural deep dives!";
    public string Hashtags { get; set; } = "#KeshavSingh #AIAnimation #HinduPanchang #WebDev";
    public string DefaultDestinationBaseUrl { get; set; } = "https://studio.keshavsingh.in";
    public string AdminPortalUrl { get; set; } = "https://admin.keshavsingh.in";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
