using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly GrowthAdvisorService _advisorService;
    private readonly ChannelLookupService _lookupService;

    public RecommendationsController(GrowthAdvisorService advisorService, ChannelLookupService lookupService)
    {
        _advisorService = advisorService;
        _lookupService = lookupService;
    }

    [HttpGet("audit")]
    public async Task<ActionResult<ChannelAuditResult>> Audit(
        [FromQuery] string handle = "",
        [FromQuery] SocialPlatform platform = SocialPlatform.YouTube,
        CancellationToken ct = default)
    {
        var userId = User.GetCallerUserId();
        var isAdmin = User.IsCallerAdmin();

        if (!string.IsNullOrWhiteSpace(handle) && string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                var lookup = await _lookupService.AnalyzeAsync(new PublicChannelLookupRequest
                {
                    Query = handle,
                    Platform = platform
                }, ct);
                return Ok(lookup.Audit);
            }
            catch
            {
                // Fall back to benchmark audit below if lookup fails
            }
        }

        var result = await _advisorService.AuditChannelAsync(handle, platform, userId, isAdmin);
        if (!string.IsNullOrWhiteSpace(handle) && result.HealthScoreOutOf100 == 76.0)
        {
            try
            {
                var lookup = await _lookupService.AnalyzeAsync(new PublicChannelLookupRequest
                {
                    Query = handle,
                    Platform = platform
                }, ct);
                return Ok(lookup.Audit);
            }
            catch
            {
                // Return baseline benchmark audit
            }
        }

        return Ok(result);
    }

    [HttpGet("config")]
    public async Task<ActionResult<GrowthConfig>> GetConfig()
    {
        var config = await _advisorService.GetGrowthConfigAsync();
        return Ok(config);
    }
}
