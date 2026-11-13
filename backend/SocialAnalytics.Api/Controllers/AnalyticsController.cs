using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsEngineService _analyticsService;
    private readonly ChannelLookupService _lookupService;
    private readonly SocialConfigService _configService;
    private readonly YouTubeLiveFetchService _youtubeFetch;

    public AnalyticsController(
        AnalyticsEngineService analyticsService,
        ChannelLookupService lookupService,
        SocialConfigService configService,
        YouTubeLiveFetchService youtubeFetch)
    {
        _analyticsService = analyticsService;
        _lookupService = lookupService;
        _configService = configService;
        _youtubeFetch = youtubeFetch;
    }

    /// <summary>
    /// Returns only channels owned by the signed-in caller. Anonymous visitors receive
    /// an empty list so Admin/other users' private channels and revenues are never exposed.
    /// </summary>
    [HttpGet("channels")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ChannelMetrics>>> GetChannels()
    {
        var channels = await _analyticsService.GetChannelsForUserAsync(
            User.GetCallerUserId(),
            User.IsCallerAdmin());
        return Ok(channels);
    }

    [HttpGet("overview")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PlatformSummary>>> GetOverview()
    {
        var summaries = await _analyticsService.GetPlatformSummariesForUserAsync(
            User.GetCallerUserId(),
            User.IsCallerAdmin());
        return Ok(summaries);
    }

    [HttpGet("videos")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecentFeedVideo>>> GetRecentVideos([FromQuery] int limit = 24)
    {
        var videos = await _analyticsService.GetRecentVideosForUserAsync(
            User.GetCallerUserId(),
            User.IsCallerAdmin(),
            limit);
        return Ok(videos);
    }

    /// <summary>
    /// Public auto-search on YouTube / social handles so visitors and signed-in creators
    /// can find any channel by @handle, URL, or name with minimal manual input.
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ChannelSearchCandidate>>> SearchChannels(
        [FromQuery] string q,
        [FromQuery] SocialPlatform platform = SocialPlatform.YouTube,
        [FromQuery] int limit = 6,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new List<ChannelSearchCandidate>());

        var results = await _lookupService.SearchAsync(q, platform, limit, ct);
        return Ok(results);
    }

    /// <summary>
    /// Live public channel audit &amp; earnings estimation by handle/URL/name without reading or
    /// revealing owner/admin private channel databases.
    /// </summary>
    [HttpPost("lookup")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicChannelAnalysisResult>> LookupChannel(
        [FromBody] PublicChannelLookupRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _lookupService.AnalyzeAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("my-channels/auto-add")]
    [Authorize]
    public async Task<ActionResult<ChannelMetrics>> AutoAddMyChannel(
        [FromBody] PublicChannelLookupRequest request,
        CancellationToken ct = default)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        try
        {
            var analysis = await _lookupService.AnalyzeAsync(request, ct);
            analysis.Channel.Id = string.Empty;
            var saved = await _analyticsService.UpsertUserChannelAsync(userId, User.IsCallerAdmin(), analysis.Channel);
            return saved is not null ? Ok(saved) : Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("my-channels")]
    [Authorize]
    public async Task<ActionResult<ChannelMetrics>> SaveMyChannel([FromBody] ChannelMetrics channel, CancellationToken ct)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        if (channel.Platform == SocialPlatform.YouTube && !channel.IsVerifiedLive && !string.IsNullOrWhiteSpace(channel.ChannelHandle))
        {
            var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
            await _youtubeFetch.EnrichChannelAsync(channel, apiKey, ct);
        }

        var saved = await _analyticsService.UpsertUserChannelAsync(userId, User.IsCallerAdmin(), channel);
        return saved is not null ? Ok(saved) : Forbid();
    }

    [HttpPost("my-channels/{id}/refresh")]
    [Authorize]
    public async Task<ActionResult<ChannelMetrics>> RefreshMyChannel(string id, CancellationToken ct)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var existing = await _analyticsService.GetOwnedChannelAsync(userId, User.IsCallerAdmin(), id);
        if (existing is null) return NotFound();

        if (existing.Platform == SocialPlatform.YouTube)
        {
            var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
            await _youtubeFetch.EnrichChannelAsync(existing, apiKey, ct);
        }

        var updated = await _analyticsService.UpsertUserChannelAsync(userId, User.IsCallerAdmin(), existing);
        return updated is not null ? Ok(updated) : NotFound();
    }

    [HttpDelete("my-channels/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMyChannel(string id)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var deleted = await _analyticsService.DeleteUserChannelAsync(userId, User.IsCallerAdmin(), id);
        return deleted ? NoContent() : NotFound();
    }
}
