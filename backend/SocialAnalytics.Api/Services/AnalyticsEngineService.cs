using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class AnalyticsEngineService
{
    private static readonly List<ChannelMetrics> MockChannels = new()
    {
        new ChannelMetrics
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
            GrowthRateMonthlyPercentage = 12.5
        },
        new ChannelMetrics
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
            GrowthRateMonthlyPercentage = 18.2
        },
        new ChannelMetrics
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
            GrowthRateMonthlyPercentage = 6.4
        }
    };

    public Task<List<ChannelMetrics>> GetAllChannelsAsync()
    {
        return Task.FromResult(MockChannels);
    }

    public Task<List<PlatformSummary>> GetPlatformSummariesAsync()
    {
        var summaries = new List<PlatformSummary>
        {
            new PlatformSummary
            {
                Platform = SocialPlatform.YouTube,
                TotalAudience = 45200,
                TotalViews = 384000,
                AvgEngagement = 7.4,
                EstimatedEarningsMonthly = 1450m
            },
            new PlatformSummary
            {
                Platform = SocialPlatform.Instagram,
                TotalAudience = 28600,
                TotalViews = 512000,
                AvgEngagement = 8.9,
                EstimatedEarningsMonthly = 850m
            },
            new PlatformSummary
            {
                Platform = SocialPlatform.Facebook,
                TotalAudience = 19400,
                TotalViews = 180000,
                AvgEngagement = 5.2,
                EstimatedEarningsMonthly = 420m
            }
        };

        return Task.FromResult(summaries);
    }
}
