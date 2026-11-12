using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public class RevenueCalculatorService
{
    private readonly SocialConfigService _configService;

    public RevenueCalculatorService(SocialConfigService configService)
    {
        _configService = configService;
    }

    public async Task<EarningsReport> CalculateEarningsAsync(EarningsCalculationRequest request)
    {
        var config = await _configService.GetEarningsConfigAsync();

        // Standard formula: (Views / 1000) * RPM
        var adRevenue = (request.MonthlyViews / 1000m) * request.EstimatedRpm;
        var sponsorshipRevenue = request.MonthlySponsorshipDeals * request.AvgSponsorshipRate;

        var estimatedBuyers = request.MonthlyViews * (request.AffiliateConversionRatePercentage / 100m);
        var affiliateRevenue = estimatedBuyers * request.AvgAffiliatePayout;

        var totalMonthly = adRevenue + sponsorshipRevenue + affiliateRevenue;
        var totalYearly = totalMonthly * 12m;

        var effectiveRpm = request.MonthlyViews > 0
            ? (totalMonthly / request.MonthlyViews) * 1000m
            : 0m;

        var opportunities = config.Opportunities
            .Where(o => string.IsNullOrWhiteSpace(o.Platform)
                || o.Platform.Equals("All", StringComparison.OrdinalIgnoreCase)
                || o.Platform.Equals(request.Platform.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(o => $"{o.Title}: {o.Description}")
            .ToList();

        if (opportunities.Count == 0)
        {
            opportunities = config.Opportunities
                .Select(o => $"{o.Title}: {o.Description}")
                .ToList();
        }

        return new EarningsReport
        {
            MonthlyViews = request.MonthlyViews,
            AdRevenueMonthly = Math.Round(adRevenue, 2),
            SponsorshipRevenueMonthly = Math.Round(sponsorshipRevenue, 2),
            AffiliateRevenueMonthly = Math.Round(affiliateRevenue, 2),
            TotalEstimatedMonthlyRevenue = Math.Round(totalMonthly, 2),
            TotalEstimatedYearlyRevenue = Math.Round(totalYearly, 2),
            EffectiveRpm = Math.Round(effectiveRpm, 2),
            Currency = "USD",
            Streams =
            [
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
            ],
            MonetizationOpportunities = opportunities
        };
    }

    public async Task<EarningsReport> GetDefaultProjectionAsync()
    {
        var config = await _configService.GetEarningsConfigAsync();
        var primary = config.PlatformBenchmarks.FirstOrDefault();
        var defaultNiche = config.Niches.FirstOrDefault();

        var req = primary is null
            ? new EarningsCalculationRequest()
            : new EarningsCalculationRequest
            {
                Platform = Enum.TryParse<SocialPlatform>(primary.Platform, true, out var parsed)
                    ? parsed
                    : SocialPlatform.YouTube,
                Niche = defaultNiche?.Name ?? "Tech & Education",
                MonthlyViews = primary.DefaultMonthlyViews,
                EstimatedRpm = (decimal)primary.DefaultRpmUsd,
                MonthlySponsorshipDeals = 1,
                AvgSponsorshipRate = (decimal)primary.DefaultSponsorshipUsd,
                AffiliateConversionRatePercentage = (decimal)primary.AffiliateConversionRatePercent,
                AvgAffiliatePayout = (decimal)primary.AvgOrderValueUsd
            };

        return await CalculateEarningsAsync(req);
    }
}
