using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly GrowthAdvisorService _advisorService;

    public RecommendationsController(GrowthAdvisorService advisorService)
    {
        _advisorService = advisorService;
    }

    [HttpGet("audit")]
    public async Task<ActionResult<ChannelAuditResult>> Audit(
        [FromQuery] string handle = "@KeshavSingh",
        [FromQuery] SocialPlatform platform = SocialPlatform.YouTube)
    {
        var result = await _advisorService.AuditChannelAsync(handle, platform);
        return Ok(result);
    }

    [HttpGet("config")]
    public async Task<ActionResult<GrowthConfig>> GetConfig()
    {
        var config = await _advisorService.GetGrowthConfigAsync();
        return Ok(config);
    }
}
