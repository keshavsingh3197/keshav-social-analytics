using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class GrowthAdvisorService
{
    public ChannelAuditResult AuditChannel(string channelHandle, SocialPlatform platform)
    {
        var suggestions = new List<GrowthSuggestion>
        {
            new()
            {
                Category = "Retention & Drop-off Fix",
                Title = "Optimize the First 30 Seconds (The Hook)",
                Description = "Viewers are dropping off in the initial 25 seconds. Introduce the payoff earlier and cut introductory animations.",
                Impact = SuggestionImpact.High,
                ActionItem = "Start videos with a direct question or visual demonstration within 5 seconds before giving context.",
                EstimatedGain = "+24% Viewer Retention",
                ApplicablePlatform = platform
            },
            new()
            {
                Category = "SEO & Title CTR",
                Title = "Use Curiosity & High-Intent Numbers in Titles",
                Description = "Titles containing high-contrast comparisons or specific numbers show 38% higher click-through rates.",
                Impact = SuggestionImpact.High,
                ActionItem = "Change vague titles to outcome-driven ones: e.g. 'How I Built 3 Projects in 24h with AI' instead of 'Project updates'.",
                EstimatedGain = "+35% Impressions CTR",
                ApplicablePlatform = platform
            },
            new()
            {
                Category = "Upload Timing",
                Title = "Publish During Peak Regional Audience Activity",
                Description = "Your audience is predominantly active between 6:30 PM - 9:00 PM IST (13:00 - 15:30 UTC) on weekdays and 10:00 AM Sundays.",
                Impact = SuggestionImpact.Medium,
                ActionItem = "Schedule upcoming videos 45 minutes prior to peak activity (5:45 PM IST) so the algorithm indexes the video before the wave.",
                EstimatedGain = "+18% Initial 2-Hour Velocity",
                ApplicablePlatform = platform
            },
            new()
            {
                Category = "Traffic Funneling",
                Title = "Add Pinned Comment & Description CTA for Sibling Websites",
                Description = "Funneling viewers to studio.keshavsingh.in and panchang.keshavsingh.in creates a 3.4x lifetime value per viewer.",
                Impact = SuggestionImpact.High,
                ActionItem = "Place your tracked link at line 1 of the description and pin the same link in the top comment.",
                EstimatedGain = "+450 Monthly Visits / Video",
                ApplicablePlatform = platform
            }
        };

        return new ChannelAuditResult
        {
            ChannelHandle = channelHandle,
            HealthScoreOutOf100 = 84.5,
            PrioritySuggestions = suggestions,
            BestUploadTimes = new List<string>
            {
                "Tuesday: 6:30 PM - 8:30 PM IST",
                "Thursday: 7:00 PM - 9:00 PM IST",
                "Saturday: 11:00 AM - 1:00 PM IST",
                "Sunday: 10:00 AM - 12:30 PM IST"
            },
            HighDemandKeywords = new List<string>
            {
                "AI animation tools",
                "Hindu panchang today",
                "Daily rashifal hindi",
                "Creator monetization tips",
                "Full stack .NET Angular",
                "YouTube earnings CPM"
            }
        };
    }
}
