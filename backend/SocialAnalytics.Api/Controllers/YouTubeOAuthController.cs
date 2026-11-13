using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/integrations/youtube")]
public class YouTubeOAuthController : ControllerBase
{
    private readonly YouTubeOAuthService _oauthService;

    public YouTubeOAuthController(YouTubeOAuthService oauthService)
    {
        _oauthService = oauthService;
    }

    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<ActionResult<YouTubeOAuthConfigPublicView>> GetPublicConfig()
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        return Ok(await _oauthService.GetPublicConfigAsync(origin));
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<YouTubeOAuthStatusResponse>> GetStatus()
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var origin = Request.Headers.Origin.FirstOrDefault();
        var cfg = await _oauthService.GetPublicConfigAsync(origin);
        var connections = await _oauthService.ListConnectionsForUserAsync(userId);
        return Ok(new YouTubeOAuthStatusResponse(cfg.IsOAuthConfigured, connections));
    }

    [HttpPost("oauth/start")]
    [Authorize]
    public async Task<ActionResult<YouTubeOAuthStartResponse>> StartConnect([FromBody] YouTubeOAuthStartRequest? request)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        try
        {
            var origin = Request.Headers.Origin.FirstOrDefault();
            var url = await _oauthService.BeginConnectAsync(userId, request?.ReturnPath, origin);
            return Ok(new YouTubeOAuthStartResponse(url));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("oauth/complete")]
    [Authorize]
    public async Task<ActionResult<YouTubeOAuthCompleteResponse>> CompleteConnect(
        [FromBody] YouTubeOAuthCompleteRequest request,
        CancellationToken ct)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        try
        {
            var result = await _oauthService.CompleteConnectAsync(
                userId,
                User.IsCallerAdmin(),
                request.Code,
                request.State,
                ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("connections/{channelId}/sync")]
    [Authorize]
    public async Task<ActionResult<ChannelMetrics>> SyncConnection(string channelId, CancellationToken ct)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var updated = await _oauthService.SyncConnectionAsync(userId, User.IsCallerAdmin(), channelId, ct);
        return updated is not null ? Ok(updated) : NotFound(new { message = "Connected YouTube channel not found or failed to refresh." });
    }

    [HttpDelete("connections/{channelId}")]
    [Authorize]
    public async Task<IActionResult> Disconnect(string channelId, CancellationToken ct)
    {
        var userId = User.GetCallerUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var removed = await _oauthService.DisconnectAsync(userId, channelId, ct);
        return removed ? NoContent() : NotFound();
    }
}
