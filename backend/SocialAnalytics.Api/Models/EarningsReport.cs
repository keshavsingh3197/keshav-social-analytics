namespace SocialAnalytics.Api.Models;

public class EarningsCalculationRequest
{
    public SocialPlatform Platform { get; set; } = SocialPlatform.YouTube;
    public string Niche { get; set; } = "Tech & Education";
    public long MonthlyViews { get; set; } = 100000;
    public decimal EstimatedRpm { get; set; } = 3.50m; // Revenue per 1000 views in USD
    public int MonthlySponsorshipDeals { get; set; } = 1;
    public decimal AvgSponsorshipRate { get; set; } = 500m;
    public decimal AffiliateConversionRatePercentage { get; set; } = 0.5m;
    public decimal AvgAffiliatePayout { get; set; } = 15m;
}

public class EarningsReport
{
    public long MonthlyViews { get; set; }
    public decimal AdRevenueMonthly { get; set; }
    public decimal SponsorshipRevenueMonthly { get; set; }
    public decimal AffiliateRevenueMonthly { get; set; }
    public decimal TotalEstimatedMonthlyRevenue { get; set; }
    public decimal TotalEstimatedYearlyRevenue { get; set; }
    public decimal EffectiveRpm { get; set; }
    public string Currency { get; set; } = "USD";
    public List<RevenueStreamBreakdown> Streams { get; set; } = new();
    public List<string> MonetizationOpportunities { get; set; } = new();
}

public class RevenueStreamBreakdown
{
    public string StreamName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double PercentageShare { get; set; }
}
