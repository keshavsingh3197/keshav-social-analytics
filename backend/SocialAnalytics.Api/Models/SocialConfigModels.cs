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
    public string BrandName { get; set; } = "SocialPulse";
    public string BrandSubtitle { get; set; } = "Creator Analytics";
    public string HeroTitle { get; set; } = "Social Channel Performance & Growth Hub";
    public string HeroSubtitle { get; set; } = "Track live engagement across YouTube, Instagram, and Facebook. Maximize audience retention, calculate creator earnings, and funnel viewer traffic across your connected apps.";
    public string YouTubeSnippetHeader { get; set; } = "Explore Connected Apps & Resources:";
    public string YouTubeSnippetFooter { get; set; } = "Subscribe for regular updates & new releases!";
    public string Hashtags { get; set; } = string.Empty;
    public string DefaultDestinationBaseUrl { get; set; } = string.Empty;
    public string EcosystemConfigUrl { get; set; } = "https://id.keshavsingh.in/api/config";
    public string AdminPortalUrl { get; set; } = "https://admin.keshavsingh.in";
    public AdMonetizationConfig Ads { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AdMonetizationConfig
{
    public bool AdsEnabled { get; set; }
    public string AdSenseClientId { get; set; } = string.Empty;
    public string HeaderAdSlotId { get; set; } = string.Empty;
    public string InFeedAdSlotId { get; set; } = string.Empty;
    public string SidebarAdSlotId { get; set; } = string.Empty;
    public string FooterAdSlotId { get; set; } = string.Empty;
    public string AdLabel { get; set; } = "Advertisement";
    public bool SponsorBannerEnabled { get; set; }
    public string SponsorBadge { get; set; } = "Featured";
    public string SponsorTitle { get; set; } = string.Empty;
    public string SponsorDescription { get; set; } = string.Empty;
    public string SponsorCtaText { get; set; } = "Learn More";
    public string SponsorUrl { get; set; } = string.Empty;
}

public class IntegrationSecrets
{
    public string Id { get; set; } = "default";
    public string? YouTubeApiKey { get; set; }
    public string? GoogleClientId { get; set; }
    public string? GoogleClientSecret { get; set; }
    public string? OAuthRedirectUri { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class IntegrationSettingsView
{
    public bool HasYouTubeApiKey { get; set; }
    public string? YouTubeApiKeyMasked { get; set; }
    public bool HasGoogleClientSecret { get; set; }
    public string? GoogleClientSecretMasked { get; set; }
    public string? GoogleClientId { get; set; }
    public string? OAuthRedirectUri { get; set; }
    public bool IsYouTubeOAuthConfigured { get; set; }
    public bool ManagedByCentralAdmin { get; set; }
    public bool CentralYouTubeApiKeyConfigured { get; set; }
    public bool CentralGoogleOAuthConfigured { get; set; }
    public bool CentralAdSenseConfigured { get; set; }
    public string? AdSenseClientId { get; set; }
    public string EcosystemConfigUrl { get; set; } = string.Empty;
    public string AdminPortalUrl { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class UpdateIntegrationSettingsRequest
{
    public string? YouTubeApiKey { get; set; }
    public bool ClearYouTubeApiKey { get; set; }
    public string? GoogleClientId { get; set; }
    public string? GoogleClientSecret { get; set; }
    public bool ClearGoogleOAuth { get; set; }
    public string? OAuthRedirectUri { get; set; }
    public string? EcosystemConfigUrl { get; set; }
    public string? AdminPortalUrl { get; set; }
}

public class CentralConfigEnvelope
{
    public string? SiteTitle { get; set; }
    public string? BlogUrl { get; set; }
    public Dictionary<string, string>? Values { get; set; }
    public List<CentralWebsiteDto>? Websites { get; set; }
    public List<CentralChannelDto>? Channels { get; set; }
}

public class CentralWebsiteDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool ShowInEcosystemBar { get; set; }
}

public class CentralChannelDto
{
    public string? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string Platform { get; set; } = "YouTube";
    public string? ChannelId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Niche { get; set; }
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public List<string>? Hashtags { get; set; }
    public bool ShowInAnalytics { get; set; }
    public bool IsEnabled { get; set; }
}

public class EcosystemSyncSummary
{
    public int ImportedChannels { get; set; }
    public int UpdatedChannels { get; set; }
    public int LiveVideosFetched { get; set; }
    public int FunnelsCreated { get; set; }
    public int PurgedLegacyRecords { get; set; }
    public bool UsedYouTubeApiKey { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
