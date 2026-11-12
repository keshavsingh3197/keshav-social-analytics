namespace SocialAnalytics.Api.Models;

public class FunnelCampaign
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string SourcePlatform { get; set; } = string.Empty; // "YouTube Description", "Instagram Bio", "Facebook Post"
    public string DestinationUrl { get; set; } = string.Empty; // e.g. "https://studio.keshavsingh.in" or "https://panchang.keshavsingh.in"
    public string TrackingSlug { get; set; } = string.Empty;
    public long TotalClicks { get; set; }
    public long Conversions { get; set; }
    public double ConversionRatePercentage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
