using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsEngineService _analyticsService;

    public AnalyticsController(AnalyticsEngineService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("channels")]
    public async Task<ActionResult<List<ChannelMetrics>>> GetChannels()
    {
        var channels = await _analyticsService.GetAllChannelsAsync();
        return Ok(channels);
    }

    [HttpGet("overview")]
    public async Task<ActionResult<List<PlatformSummary>>> GetOverview()
    {
        var summaries = await _analyticsService.GetPlatformSummariesAsync();
        return Ok(summaries);
    }
}
