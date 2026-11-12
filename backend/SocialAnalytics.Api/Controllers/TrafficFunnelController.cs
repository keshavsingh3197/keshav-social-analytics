using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrafficFunnelController : ControllerBase
{
    private static readonly List<FunnelCampaign> Campaigns = new()
    {
        new FunnelCampaign
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
        new FunnelCampaign
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
        new FunnelCampaign
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
    };

    [HttpGet("campaigns")]
    public ActionResult<List<FunnelCampaign>> GetCampaigns()
    {
        return Ok(Campaigns);
    }

    [HttpPost("campaigns")]
    public ActionResult<FunnelCampaign> CreateCampaign([FromBody] FunnelCampaign campaign)
    {
        campaign.Id = Guid.NewGuid().ToString();
        campaign.CreatedAt = DateTime.UtcNow;
        Campaigns.Add(campaign);
        return CreatedAtAction(nameof(GetCampaigns), new { id = campaign.Id }, campaign);
    }

    [HttpPost("track/{slug}")]
    public IActionResult TrackClick(string slug)
    {
        var campaign = Campaigns.FirstOrDefault(c => c.TrackingSlug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        if (campaign != null)
        {
            campaign.TotalClicks++;
            return Ok(new { success = true, destination = campaign.DestinationUrl, totalClicks = campaign.TotalClicks });
        }
        return NotFound(new { message = "Campaign slug not found" });
    }
}
