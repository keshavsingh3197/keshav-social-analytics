using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public static class SocialSeedData
{
    public static List<ChannelMetrics> DefaultChannels() =>
    [
        new()
        {
            Id = "ch-1",
            ChannelName = "Keshav Singh Official",
            ChannelHandle = "@KeshavSingh",
            Platform = SocialPlatform.YouTube,
            Niche = "AI, Animation & Dev",
            FollowersOrSubscribers = 45200,
            TotalViewsLast30Days = 384000,
            EngagementRatePercentage = 7.4,
            AverageViewDurationMinutes = 4.8,
            VideosOrPostsCount = 142,
            GrowthRateMonthlyPercentage = 12.5,
            RpmUsd = 3.78,
            EstimatedMonthlyRevenue = 1450,
            ChannelUrl = "https://www.youtube.com/@KeshavSingh",
            IsActive = true
        },
        new()
        {
            Id = "ch-2",
            ChannelName = "Keshav Dev & Stories",
            ChannelHandle = "@KeshavStories",
            Platform = SocialPlatform.Instagram,
            Niche = "Culture & Short Stories",
            FollowersOrSubscribers = 28600,
            TotalViewsLast30Days = 512000,
            EngagementRatePercentage = 8.9,
            AverageViewDurationMinutes = 0.9,
            VideosOrPostsCount = 210,
            GrowthRateMonthlyPercentage = 18.2,
            RpmUsd = 1.66,
            EstimatedMonthlyRevenue = 850,
            ChannelUrl = "https://www.instagram.com/",
            IsActive = true
        },
        new()
        {
            Id = "ch-3",
            ChannelName = "Keshav Cultural Page",
            ChannelHandle = "keshav.cultural",
            Platform = SocialPlatform.Facebook,
            Niche = "Vedic Panchang & History",
            FollowersOrSubscribers = 19400,
            TotalViewsLast30Days = 180000,
            EngagementRatePercentage = 5.2,
            AverageViewDurationMinutes = 2.1,
            VideosOrPostsCount = 88,
            GrowthRateMonthlyPercentage = 6.4,
            RpmUsd = 2.33,
            EstimatedMonthlyRevenue = 420,
            ChannelUrl = "https://www.facebook.com/",
            IsActive = true
        }
    ];

    public static List<FunnelCampaign> DefaultCampaigns() =>
    [
        new()
        {
            Id = "f-1",
            Title = "AnimStudio AI Promotion from YouTube",
            SourcePlatform = "YouTube Description",
            DestinationUrl = "https://studio.keshavsingh.in",
            TrackingSlug = "yt-animstudio",
            TotalClicks = 2840,
            Conversions = 312,
            ConversionRatePercentage = 11.0,
            IsActive = true
        },
        new()
        {
            Id = "f-2",
            Title = "Daily Panchang & Rashifal Link from Instagram",
            SourcePlatform = "Instagram Bio",
            DestinationUrl = "https://panchang.keshavsingh.in",
            TrackingSlug = "ig-panchang",
            TotalClicks = 4920,
            Conversions = 890,
            ConversionRatePercentage = 18.1,
            IsActive = true
        },
        new()
        {
            Id = "f-3",
            Title = "Dev Blog Articles Cross-promotion",
            SourcePlatform = "Facebook Page",
            DestinationUrl = "https://git.keshavsingh.in",
            TrackingSlug = "fb-blog",
            TotalClicks = 1150,
            Conversions = 140,
            ConversionRatePercentage = 12.2,
            IsActive = true
        }
    ];

    public static List<GrowthSuggestion> DefaultSuggestions() =>
    [
        new()
        {
            Id = "gs-1",
            Category = "Retention & Drop-off Fix",
            Title = "Optimize the First 30 Seconds (The Hook)",
            Description = "Viewers drop off in the initial 25 seconds if the payoff is delayed. Show the outcome first.",
            Impact = SuggestionImpact.High,
            ActionItem = "Start videos with a direct question or visual demonstration within 5 seconds before intro animations.",
            EstimatedGain = "+24% Viewer Retention",
            ApplicablePlatform = SocialPlatform.YouTube
        },
        new()
        {
            Id = "gs-2",
            Category = "SEO & Title CTR",
            Title = "Use Curiosity & High-Intent Numbers in Titles",
            Description = "Titles containing high-contrast comparisons or concrete numbers yield stronger impressions CTR.",
            Impact = SuggestionImpact.High,
            ActionItem = "Switch vague titles to outcome-driven titles: e.g. 'How I Built 3 Apps in 24h with AI'.",
            EstimatedGain = "+35% Impressions CTR",
            ApplicablePlatform = SocialPlatform.YouTube
        },
        new()
        {
            Id = "gs-3",
            Category = "Upload Timing",
            Title = "Publish During Peak Regional Audience Activity",
            Description = "Primary viewers peak between 6:30 PM - 9:00 PM IST weekdays and 10:00 AM IST Sundays.",
            Impact = SuggestionImpact.Medium,
            ActionItem = "Schedule uploads 45 minutes prior to peak traffic (5:45 PM IST) for early indexing.",
            EstimatedGain = "+18% Initial 2-Hour Velocity",
            ApplicablePlatform = SocialPlatform.Instagram
        },
        new()
        {
            Id = "gs-4",
            Category = "Traffic Funneling",
            Title = "Add Pinned Comment & Description CTA for Ecosystem Sites",
            Description = "Routing viewers to studio.keshavsingh.in and panchang.keshavsingh.in multiplies session value.",
            Impact = SuggestionImpact.High,
            ActionItem = "Place a tracked /t/<slug> funnel link in line 1 of the video description and pinned comment.",
            EstimatedGain = "+450 Monthly Visits / Video",
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
                DefaultMonthlyViews = 384000,
                DefaultRpmUsd = 3.78,
                DefaultSponsorshipUsd = 500,
                AffiliateConversionRatePercent = 0.6,
                AvgOrderValueUsd = 18,
                Description = "Long-form YouTube videos with mid-roll ads, sponsorships, and pinned CTA funnels."
            },
            new()
            {
                Platform = "Instagram",
                Icon = "📸",
                DefaultMonthlyViews = 512000,
                DefaultRpmUsd = 1.66,
                DefaultSponsorshipUsd = 350,
                AffiliateConversionRatePercent = 0.9,
                AvgOrderValueUsd = 15,
                Description = "Reels discovery + bio/story link funnels into Studio AI & Hindu Panchang."
            },
            new()
            {
                Platform = "Facebook",
                Icon = "📘",
                DefaultMonthlyViews = 180000,
                DefaultRpmUsd = 2.33,
                DefaultSponsorshipUsd = 200,
                AffiliateConversionRatePercent = 0.5,
                AvgOrderValueUsd = 12,
                Description = "In-stream video ads and daily shareable cultural/tech reels."
            },
            new()
            {
                Platform = "LinkedIn",
                Icon = "💼",
                DefaultMonthlyViews = 95000,
                DefaultRpmUsd = 0.80,
                DefaultSponsorshipUsd = 650,
                AffiliateConversionRatePercent = 1.2,
                AvgOrderValueUsd = 35,
                Description = "B2B engineering carousels & high-value SaaS/consulting leads."
            }
        ],
        Niches =
        [
            new() { Id = "n-1", Name = "Tech, AI & Software Engineering", MinRpmUsd = 3.5, MaxRpmUsd = 9.0, SuggestedRpmUsd = 5.2, Tip = "High SaaS & AI tool advertiser bids." },
            new() { Id = "n-2", Name = "Finance & Investing", MinRpmUsd = 6.0, MaxRpmUsd = 15.0, SuggestedRpmUsd = 8.5, Tip = "Highest AdSense RPM category; pair with GharLedger workflows." },
            new() { Id = "n-3", Name = "Hindu Panchang & Spirituality", MinRpmUsd = 1.2, MaxRpmUsd = 3.2, SuggestedRpmUsd = 2.1, Tip = "Massive repeat daily traffic; pair with panchang.keshavsingh.in." },
            new() { Id = "n-4", Name = "2D/3D AI Animation & Storytelling", MinRpmUsd = 2.0, MaxRpmUsd = 5.5, SuggestedRpmUsd = 3.5, Tip = "Strong binge-watch retention and studio.keshavsingh.in funnels." },
            new() { Id = "n-5", Name = "Competitive Exam Prep (Pratiyogita)", MinRpmUsd = 1.5, MaxRpmUsd = 4.0, SuggestedRpmUsd = 2.6, Tip = "High conversion into test series and study portals." }
        ],
        Opportunities =
        [
            new() { Id = "opp-1", Title = "Cross-Promote Keshav AnimStudio AI", Platform = "YouTube", Impact = "High", Description = "Route AI & animation viewers to studio.keshavsingh.in via description links.", ActionUrl = "https://studio.keshavsingh.in" },
            new() { Id = "opp-2", Title = "Daily Hindu Panchang Traffic Loop", Platform = "Instagram", Impact = "High", Description = "Convert daily Tithi & Rashifal viewers to recurring panchang.keshavsingh.in sessions.", ActionUrl = "https://panchang.keshavsingh.in" },
            new() { Id = "opp-3", Title = "Recurring Templates & Memberships", Platform = "YouTube", Impact = "Medium", Description = "Bundle prompt packs, storyboards, and engineering notes for predictable MRR.", ActionUrl = "https://git.keshavsingh.in" },
            new() { Id = "opp-4", Title = "Multi-Video Sponsor Packages", Platform = "All", Impact = "High", Description = "Negotiate 3-video integration bundles across YouTube + Reels to lift deal rates 25-40%.", ActionUrl = "https://keshavsingh.in" }
        ]
    };

    public static GrowthConfig DefaultGrowthConfig() => new()
    {
        Id = "default",
        TargetEngagementPercent = 6.5,
        TargetAvgWatchMinutes = 5.0,
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
            "Hindu panchang today",
            "Daily rashifal hindi",
            "Creator monetization tips",
            "Full stack .NET Angular",
            "YouTube earnings CPM"
        ]
    };

    public static SiteConfig DefaultSiteConfig() => new()
    {
        Id = "default",
        HeroTitle = "Social Channel Performance & Growth Hub",
        HeroSubtitle = "Track multi-platform engagement across YouTube, Instagram, and Facebook. Maximize audience retention, calculate creator earnings, and funnel viewer traffic directly into your monetized ecosystems.",
        YouTubeSnippetHeader = "🚀 Explore Keshav Singh Ecosystem Tools:",
        YouTubeSnippetFooter = "🔔 Subscribe for weekly AI, engineering & cultural deep dives!",
        Hashtags = "#KeshavSingh #AIAnimation #HinduPanchang #WebDev",
        DefaultDestinationBaseUrl = "https://studio.keshavsingh.in",
        AdminPortalUrl = "https://admin.keshavsingh.in"
    };
}
