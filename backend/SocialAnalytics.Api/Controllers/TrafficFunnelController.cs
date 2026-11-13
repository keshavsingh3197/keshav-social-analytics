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
    [AllowAnonymous]
    public async Task<ActionResult<List<FunnelCampaign>>> GetCampaigns()
    {
        var campaigns = await _funnelService.GetCampaignsForUserAsync(
            User.GetCallerUserId(),
            User.IsCallerAdmin(),
            activeOnly: true);
        return Ok(campaigns);
    }

    [HttpPost("campaigns")]
    [Authorize]
    public async Task<ActionResult<FunnelCampaign>> CreateCampaign([FromBody] FunnelCampaign campaign)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var created = await _funnelService.UpsertUserCampaignAsync(userId, User.IsCallerAdmin(), campaign);
        if (created is null) return Forbid();
        return CreatedAtAction(nameof(GetCampaigns), new { id = created.Id }, created);
    }

    [HttpPost("campaigns/{id}/convert")]
    [Authorize]
    public async Task<ActionResult<FunnelCampaign>> RecordUserConversion(string id)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var updated = await _funnelService.RecordUserConversionAsync(userId, User.IsCallerAdmin(), id);
        return updated is not null ? Ok(updated) : NotFound();
    }

    [HttpDelete("campaigns/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUserCampaign(string id)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var deleted = await _funnelService.DeleteUserCampaignAsync(userId, User.IsCallerAdmin(), id);
        return deleted ? NoContent() : NotFound();
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
