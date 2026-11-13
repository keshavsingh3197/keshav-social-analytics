using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// Provides strictly neutral platform/algorithm defaults and zero fake creator metrics,
/// channels, or click counts. Real channels and ecosystem URLs are synced from central admin
/// (<see cref="CentralEcosystemSyncService"/>) and YouTube live feeds (<see cref="YouTubeLiveFetchService"/>).
/// </summary>
public static class SocialSeedData
{
    internal static readonly HashSet<string> LegacyDemoHandles = new(StringComparer.OrdinalIgnoreCase)
    {
        "@KeshavSingh",
        "@KeshavStories",
        "keshav.cultural"
    };

    internal static readonly HashSet<string> LegacyDemoChannelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ch-1", "ch-2", "ch-3"
    };

    public static List<ChannelMetrics> DefaultChannels() => [];

    public static List<FunnelCampaign> DefaultCampaigns() => [];

    public static List<GrowthSuggestion> DefaultSuggestions() =>
    [
        new()
        {
            Id = "gs-1",
            Category = "Retention & Drop-off Fix",
            Title = "Optimize the First 30 Seconds (The Hook)",
            Description = "Viewers drop off in the initial 25 seconds if the payoff is delayed. Show the outcome first.",
            Impact = SuggestionImpact.High,
            ActionItem = "Start videos with a direct visual payoff or hook within 5 seconds before intro animations.",
            EstimatedGain = "+24% Viewer Retention",
            ApplicablePlatform = SocialPlatform.YouTube
        },
        new()
        {
            Id = "gs-2",
            Category = "SEO & Title CTR",
            Title = "Use Clear Outcome & Search Intent Keywords in Titles",
            Description = "Titles aligned with high-intent search queries and clear framing yield stronger impressions CTR.",
            Impact = SuggestionImpact.High,
            ActionItem = "Pair strong primary topic keywords in the first 45 characters with high-contrast thumbnail text.",
            EstimatedGain = "+35% Impressions CTR",
            ApplicablePlatform = SocialPlatform.YouTube
        },
        new()
        {
            Id = "gs-3",
            Category = "Upload Timing",
            Title = "Publish Ahead of Peak Regional Audience Activity",
            Description = "Publishing 45 minutes ahead of evening audience spikes improves early notification velocity.",
            Impact = SuggestionImpact.Medium,
            ActionItem = "Schedule uploads 45 minutes prior to peak regional traffic so search indexing completes before prime time.",
            EstimatedGain = "+18% Initial 2-Hour Velocity",
            ApplicablePlatform = SocialPlatform.Instagram
        },
        new()
        {
            Id = "gs-4",
            Category = "Traffic Funneling",
            Title = "Add Pinned Comment & Description CTA for Connected Apps",
            Description = "Routing engaged viewers into your web ecosystem multiplies repeat session value.",
            Impact = SuggestionImpact.High,
            ActionItem = "Place a tracked /t/<slug> funnel link in line 1 of the video description and pinned comment.",
            EstimatedGain = "+3-8% Outbound Session Conversion",
            ApplicablePlatform = SocialPlatform.Facebook
        }
    ];

    public static EarningsConfig DefaultEarningsConfig() => new()
    {
        Id = "default",
        PlatformBenchmarks =
        [
            new()
            {
                Platform = "YouTube",
                Icon = "📺",
                DefaultMonthlyViews = 100000,
                DefaultRpmUsd = 3.20,
                DefaultSponsorshipUsd = 250,
                AffiliateConversionRatePercent = 0.5,
                AvgOrderValueUsd = 15,
                Description = "Long-form YouTube videos & Shorts feed ads, sponsorships, and pinned CTA funnels."
            },
            new()
            {
                Platform = "Instagram",
                Icon = "📸",
                DefaultMonthlyViews = 100000,
                DefaultRpmUsd = 1.40,
                DefaultSponsorshipUsd = 200,
                AffiliateConversionRatePercent = 0.8,
                AvgOrderValueUsd = 15,
                Description = "Reels discovery + bio/story link funnels into web portals."
            },
            new()
            {
                Platform = "Facebook",
                Icon = "📘",
                DefaultMonthlyViews = 100000,
                DefaultRpmUsd = 1.90,
                DefaultSponsorshipUsd = 150,
                AffiliateConversionRatePercent = 0.4,
                AvgOrderValueUsd = 12,
                Description = "In-stream video ads and shareable cultural/story reels."
            },
            new()
            {
                Platform = "LinkedIn",
                Icon = "💼",
                DefaultMonthlyViews = 50000,
                DefaultRpmUsd = 0.80,
                DefaultSponsorshipUsd = 450,
                AffiliateConversionRatePercent = 1.0,
                AvgOrderValueUsd = 25,
                Description = "B2B engineering carousels & high-value product/consulting leads."
            }
        ],
        Niches =
        [
            new() { Id = "n-1", Name = "Tech, AI & Software Engineering", MinRpmUsd = 3.5, MaxRpmUsd = 9.0, SuggestedRpmUsd = 5.2, Tip = "High SaaS & AI tool advertiser bids." },
            new() { Id = "n-2", Name = "Personal Finance & Tools", MinRpmUsd = 5.5, MaxRpmUsd = 14.0, SuggestedRpmUsd = 8.0, Tip = "Strong AdSense RPM category; pair with SaaS utility tools." },
            new() { Id = "n-3", Name = "Devotional, Bhakti & Cultural", MinRpmUsd = 1.2, MaxRpmUsd = 3.2, SuggestedRpmUsd = 2.1, Tip = "High daily repeat viewership and festival spikes." },
            new() { Id = "n-4", Name = "2D/3D AI Animation & Storytelling", MinRpmUsd = 2.0, MaxRpmUsd = 5.5, SuggestedRpmUsd = 3.5, Tip = "Strong binge-watch retention and creative suite funnels." },
            new() { Id = "n-5", Name = "Education & Exam Preparation", MinRpmUsd = 1.5, MaxRpmUsd = 4.0, SuggestedRpmUsd = 2.6, Tip = "High conversion into study portals and series." }
        ],
        Opportunities = []
    };

    public static GrowthConfig DefaultGrowthConfig() => new()
    {
        Id = "default",
        TargetEngagementPercent = 5.5,
        TargetAvgWatchMinutes = 4.5,
        BestUploadTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["YouTube"] = "Tue & Thu 6:30 PM - 8:30 PM IST | Sun 10:00 AM IST",
            ["Instagram"] = "Daily 1:00 PM IST & 7:30 PM - 9:15 PM IST",
            ["Facebook"] = "Wed, Fri, Sun 8:00 AM - 10:30 AM IST",
            ["LinkedIn"] = "Tue - Thu 9:30 AM - 11:00 AM IST"
        },
        HighDemandKeywords =
        [
            "AI animation tools",
            "Daily panchang today",
            "Devotional 3D story",
            "Creator monetization RPM"
        ]
    };

    public static SiteConfig DefaultSiteConfig() => new()
    {
        Id = "default",
        BrandName = "SocialPulse",
        BrandSubtitle = "Creator Analytics",
        HeroTitle = "Social Channel Performance & Growth Hub",
        HeroSubtitle = "Track live engagement across connected channels, inspect real recent video metrics, estimate creator RPM earnings, and route traffic across your ecosystem apps.",
        YouTubeSnippetHeader = "Explore Connected Apps & Resources:",
        YouTubeSnippetFooter = "Subscribe for regular updates & new releases!",
        Hashtags = string.Empty,
        DefaultDestinationBaseUrl = string.Empty,
        EcosystemConfigUrl = "https://id.keshavsingh.in/api/config",
        AdminPortalUrl = "https://admin.keshavsingh.in"
    };
}
