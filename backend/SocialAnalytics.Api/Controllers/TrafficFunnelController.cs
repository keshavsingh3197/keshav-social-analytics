using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrafficFunnelController : ControllerBase
{
    private readonly FunnelService _funnelService;

    public TrafficFunnelController(FunnelService funnelService)
    {
        _funnelService = funnelService;
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<List<FunnelCampaign>>> GetCampaigns()
    {
        var campaigns = await _funnelService.GetCampaignsAsync(activeOnly: true);
        return Ok(campaigns);
    }

    [HttpPost("campaigns")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FunnelCampaign>> CreateCampaign([FromBody] FunnelCampaign campaign)
    {
        var created = await _funnelService.UpsertCampaignAsync(campaign);
        return CreatedAtAction(nameof(GetCampaigns), new { id = created.Id }, created);
    }

    [HttpPost("track/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackClick(string slug)
    {
        var updated = await _funnelService.TrackClickAsync(slug);
        if (updated is null)
            return NotFound(new { message = "Campaign slug not found" });

        return Ok(new
        {
            success = true,
            destination = updated.DestinationUrl,
            totalClicks = updated.TotalClicks
        });
    }
}
