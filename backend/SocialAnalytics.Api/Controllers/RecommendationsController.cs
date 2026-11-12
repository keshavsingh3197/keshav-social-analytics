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
    public ActionResult<ChannelAuditResult> Audit([FromQuery] string handle = "@KeshavSingh", [FromQuery] SocialPlatform platform = SocialPlatform.YouTube)
    {
        var result = _advisorService.AuditChannel(handle, platform);
        return Ok(result);
    }
}
