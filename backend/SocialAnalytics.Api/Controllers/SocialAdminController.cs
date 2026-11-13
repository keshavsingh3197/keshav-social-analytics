using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class SocialAdminController : ControllerBase
{
    private readonly AnalyticsEngineService _analyticsService;
    private readonly FunnelService _funnelService;
    private readonly GrowthAdvisorService _advisorService;
    private readonly SocialConfigService _configService;
    private readonly CentralEcosystemSyncService _syncService;
    private readonly YouTubeLiveFetchService _youtubeFetch;

    public SocialAdminController(
        AnalyticsEngineService analyticsService,
        FunnelService funnelService,
        GrowthAdvisorService advisorService,
        SocialConfigService configService,
        CentralEcosystemSyncService syncService,
        YouTubeLiveFetchService youtubeFetch)
    {
        _analyticsService = analyticsService;
        _funnelService = funnelService;
        _advisorService = advisorService;
        _configService = configService;
        _syncService = syncService;
        _youtubeFetch = youtubeFetch;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var channels = await _analyticsService.GetAllChannelsAsync(includeInactive: true);
        var funnels = await _funnelService.GetCampaignsAsync(activeOnly: false);
        var suggestions = await _advisorService.GetAllSuggestionsAsync();
        var integrations = await _configService.GetIntegrationSettingsViewAsync();
        return Ok(new
        {
            storageMode = _configService.StorageMode,
            channelCount = channels.Count,
            verifiedLiveChannels = channels.Count(c => c.IsVerifiedLive),
            recentVideoCount = channels.Sum(c => c.RecentVideos?.Count ?? 0),
            funnelCount = funnels.Count,
            suggestionCount = suggestions.Count,
            hasYouTubeApiKey = integrations.HasYouTubeApiKey,
            adminUser = User.FindFirst("email")?.Value ?? User.Identity?.Name ?? "Admin"
        });
    }

    // ---- Central Ecosystem & Live YouTube Sync (Admin Only) ----

    [HttpPost("sync/ecosystem")]
    public async Task<ActionResult<EcosystemSyncSummary>> SyncEcosystem([FromQuery] bool purgeLegacy = true, CancellationToken ct = default) =>
        Ok(await _syncService.SyncAllAsync(purgeLegacy, ct));

    [HttpPost("sync/youtube")]
    public async Task<ActionResult<EcosystemSyncSummary>> SyncAllYouTube(CancellationToken ct = default) =>
        Ok(await _syncService.RefreshAllYouTubeLiveAsync(ct));

    [HttpPost("channels/{id}/sync-live")]
    public async Task<ActionResult<ChannelMetrics>> SyncSingleChannelLive(string id, CancellationToken ct = default)
    {
        var channels = await _analyticsService.GetAllChannelsAsync(includeInactive: true);
        var target = channels.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (target is null) return NotFound();

        var secrets = await _configService.GetSecretsAsync();
        await _youtubeFetch.EnrichChannelAsync(target, secrets.YouTubeApiKey, ct);
        return Ok(await _analyticsService.UpsertChannelAsync(target));
    }

    [HttpPost("purge-legacy")]
    public async Task<IActionResult> PurgeLegacyData()
    {
        var removed = await _analyticsService.PurgeLegacyDemoChannelsAsync();
        return Ok(new { purgedChannels = removed });
    }

    // ---- Integrations & API Keys (Admin Only; secrets masked on read) ----

    [HttpGet("integrations")]
    public async Task<ActionResult<IntegrationSettingsView>> GetIntegrations() =>
        Ok(await _configService.GetIntegrationSettingsViewAsync());

    [HttpPut("integrations")]
    public async Task<ActionResult<IntegrationSettingsView>> SaveIntegrations([FromBody] UpdateIntegrationSettingsRequest request) =>
        Ok(await _configService.UpdateIntegrationSettingsAsync(request));

    // ---- Channels CRUD ----

    [HttpGet("channels")]
    public async Task<ActionResult<List<ChannelMetrics>>> GetChannels() =>
        Ok(await _analyticsService.GetAllChannelsAsync(includeInactive: true));

    [HttpPost("channels")]
    public async Task<ActionResult<ChannelMetrics>> CreateChannel([FromBody] ChannelMetrics channel)
    {
        channel.OwnerUserId = string.IsNullOrWhiteSpace(channel.OwnerUserId)
            ? (User.GetCallerUserId() ?? "admin")
            : channel.OwnerUserId;
        channel.IsAdminOwned = true;
        return Ok(await _analyticsService.UpsertChannelAsync(channel));
    }

    [HttpPut("channels/{id}")]
    public async Task<ActionResult<ChannelMetrics>> UpdateChannel(string id, [FromBody] ChannelMetrics channel)
    {
        channel.Id = id;
        if (string.IsNullOrWhiteSpace(channel.OwnerUserId))
            channel.OwnerUserId = User.GetCallerUserId() ?? "admin";
        channel.IsAdminOwned = true;
        return Ok(await _analyticsService.UpsertChannelAsync(channel));
    }

    [HttpDelete("channels/{id}")]
    public async Task<IActionResult> DeleteChannel(string id)
    {
        var deleted = await _analyticsService.DeleteChannelAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // ---- Traffic Funnels CRUD ----

    [HttpGet("funnels")]
    public async Task<ActionResult<List<FunnelCampaign>>> GetFunnels() =>
        Ok(await _funnelService.GetCampaignsAsync(activeOnly: false));

    [HttpPost("funnels")]
    public async Task<ActionResult<FunnelCampaign>> CreateFunnel([FromBody] FunnelCampaign campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.OwnerUserId))
            campaign.OwnerUserId = User.GetCallerUserId() ?? "admin";
        campaign.IsAdminOwned = true;
        return Ok(await _funnelService.UpsertCampaignAsync(campaign));
    }

    [HttpPut("funnels/{id}")]
    public async Task<ActionResult<FunnelCampaign>> UpdateFunnel(string id, [FromBody] FunnelCampaign campaign)
    {
        campaign.Id = id;
        if (string.IsNullOrWhiteSpace(campaign.OwnerUserId))
            campaign.OwnerUserId = User.GetCallerUserId() ?? "admin";
        campaign.IsAdminOwned = true;
        return Ok(await _funnelService.UpsertCampaignAsync(campaign));
    }

    [HttpPost("funnels/{id}/convert")]
    public async Task<ActionResult<FunnelCampaign>> RecordConversion(string id)
    {
        var updated = await _funnelService.RecordConversionAsync(id);
        return updated is not null ? Ok(updated) : NotFound();
    }

    [HttpDelete("funnels/{id}")]
    public async Task<IActionResult> DeleteFunnel(string id)
    {
        var deleted = await _funnelService.DeleteCampaignAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // ---- Growth Suggestions & Config ----

    [HttpGet("suggestions")]
    public async Task<ActionResult<List<GrowthSuggestion>>> GetSuggestions() =>
        Ok(await _advisorService.GetAllSuggestionsAsync());

    [HttpPost("suggestions")]
    public async Task<ActionResult<GrowthSuggestion>> CreateSuggestion([FromBody] GrowthSuggestion suggestion) =>
        Ok(await _advisorService.UpsertSuggestionAsync(suggestion));

    [HttpPut("suggestions/{id}")]
    public async Task<ActionResult<GrowthSuggestion>> UpdateSuggestion(string id, [FromBody] GrowthSuggestion suggestion)
    {
        suggestion.Id = id;
        return Ok(await _advisorService.UpsertSuggestionAsync(suggestion));
    }

    [HttpDelete("suggestions/{id}")]
    public async Task<IActionResult> DeleteSuggestion(string id)
    {
        var deleted = await _advisorService.DeleteSuggestionAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("growth-config")]
    public async Task<ActionResult<GrowthConfig>> GetGrowthConfig() =>
        Ok(await _advisorService.GetGrowthConfigAsync());

    [HttpPut("growth-config")]
    public async Task<ActionResult<GrowthConfig>> SaveGrowthConfig([FromBody] GrowthConfig config) =>
        Ok(await _advisorService.SaveGrowthConfigAsync(config));

    // ---- Earnings Presets & Opportunities ----

    [HttpGet("earnings-config")]
    public async Task<ActionResult<EarningsConfig>> GetEarningsConfig() =>
        Ok(await _configService.GetEarningsConfigAsync());

    [HttpPut("earnings-config")]
    public async Task<ActionResult<EarningsConfig>> SaveEarningsConfig([FromBody] EarningsConfig config) =>
        Ok(await _configService.SaveEarningsConfigAsync(config));

    // ---- Site Config ----

    [HttpGet("site-config")]
    public async Task<ActionResult<SiteConfig>> GetSiteConfig() =>
        Ok(await _configService.GetSiteConfigAsync());

    [HttpPut("site-config")]
    public async Task<ActionResult<SiteConfig>> SaveSiteConfig([FromBody] SiteConfig config) =>
        Ok(await _configService.SaveSiteConfigAsync(config));
}
