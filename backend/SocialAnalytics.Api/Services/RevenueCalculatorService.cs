using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class RevenueCalculatorService
{
    public EarningsReport CalculateEarnings(EarningsCalculationRequest request)
    {
        // 1. Calculate Ad Revenue
        // Standard formula: (Views / 1000) * RPM
        var adRevenue = (request.MonthlyViews / 1000m) * request.EstimatedRpm;

        // 2. Calculate Sponsorships
        var sponsorshipRevenue = request.MonthlySponsorshipDeals * request.AvgSponsorshipRate;

        // 3. Calculate Affiliate / Product Sales
        // Views * conversion rate % * average payout
        var estimatedBuyers = (request.MonthlyViews * (request.AffiliateConversionRatePercentage / 100m));
        var affiliateRevenue = estimatedBuyers * request.AvgAffiliatePayout;

        var totalMonthly = adRevenue + sponsorshipRevenue + affiliateRevenue;
        var totalYearly = totalMonthly * 12m;

        var effectiveRpm = request.MonthlyViews > 0
            ? (totalMonthly / request.MonthlyViews) * 1000m
            : 0m;

        var report = new EarningsReport
        {
            MonthlyViews = request.MonthlyViews,
            AdRevenueMonthly = Math.Round(adRevenue, 2),
            SponsorshipRevenueMonthly = Math.Round(sponsorshipRevenue, 2),
            AffiliateRevenueMonthly = Math.Round(affiliateRevenue, 2),
            TotalEstimatedMonthlyRevenue = Math.Round(totalMonthly, 2),
            TotalEstimatedYearlyRevenue = Math.Round(totalYearly, 2),
            EffectiveRpm = Math.Round(effectiveRpm, 2),
            Currency = "USD",
            Streams = new List<RevenueStreamBreakdown>
            {
                new()
                {
                    StreamName = "Direct Ads / AdSense",
                    Amount = Math.Round(adRevenue, 2),
                    PercentageShare = totalMonthly > 0 ? Math.Round((double)(adRevenue / totalMonthly) * 100, 1) : 0
                },
                new()
                {
                    StreamName = "Brand Sponsorships",
                    Amount = Math.Round(sponsorshipRevenue, 2),
                    PercentageShare = totalMonthly > 0 ? Math.Round((double)(sponsorshipRevenue / totalMonthly) * 100, 1) : 0
                },
                new()
                {
                    StreamName = "Affiliate & Link Conversions",
                    Amount = Math.Round(affiliateRevenue, 2),
                    PercentageShare = totalMonthly > 0 ? Math.Round((double)(affiliateRevenue / totalMonthly) * 100, 1) : 0
                }
            },
            MonetizationOpportunities = new List<string>
            {
                "Cross-promote Keshav AnimStudio AI (studio.keshavsingh.in) via YouTube description links.",
                "Embed Hindu Panchang (panchang.keshavsingh.in) widgets for daily spiritual traffic monetization.",
                "Offer custom digital templates or community memberships with recurring monthly revenue.",
                "Negotiate multi-video brand integration packages to boost average deal size by 25-40%."
            }
        };

        return report;
    }
}
