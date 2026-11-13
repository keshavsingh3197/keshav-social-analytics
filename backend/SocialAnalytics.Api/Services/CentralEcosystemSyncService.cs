using System.Text.Json;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// Synchronizes creator channels, ecosystem websites, and funnel defaults from the central
/// Admin Identity/Config API (<c>/api/config</c>) and enriches YouTube channels via live feeds.
/// </summary>
public sealed class CentralEcosystemSyncService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly SocialConfigService _configService;
    private readonly AnalyticsEngineService _analyticsService;
    private readonly FunnelService _funnelService;
    private readonly YouTubeLiveFetchService _youtubeFetch;
    private readonly ILogger<CentralEcosystemSyncService> _logger;

    public CentralEcosystemSyncService(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        SocialConfigService configService,
        AnalyticsEngineService analyticsService,
        FunnelService funnelService,
        YouTubeLiveFetchService youtubeFetch,
        ILogger<CentralEcosystemSyncService> logger)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
        _configService = configService;
        _analyticsService = analyticsService;
        _funnelService = funnelService;
        _youtubeFetch = youtubeFetch;
        _logger = logger;
    }

    public async Task<EcosystemSyncSummary> SyncAllAsync(bool purgeLegacy = true, CancellationToken ct = default)
    {
        var summary = new EcosystemSyncSummary { SyncedAt = DateTime.UtcNow };
        var site = await _configService.GetSiteConfigAsync();
        var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
        summary.UsedYouTubeApiKey = !string.IsNullOrWhiteSpace(apiKey);

        if (purgeLegacy)
        {
            summary.PurgedLegacyRecords = await _analyticsService.PurgeLegacyDemoChannelsAsync();
        }

        var configUrl = !string.IsNullOrWhiteSpace(site.EcosystemConfigUrl)
            ? site.EcosystemConfigUrl
            : (_configuration["Ecosystem:AdminConfigUrl"] ?? "https://id.keshavsingh.in/api/config");

        CentralConfigEnvelope? envelope = null;
        try
        {
            var http = _httpFactory.CreateClient("CentralConfig");
            using var resp = await http.GetAsync(configUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                var raw = await resp.Content.ReadAsStringAsync(ct);
                envelope = JsonSerializer.Deserialize<CentralConfigEnvelope>(raw, JsonOpts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch central config from {Url}", configUrl);
        }

        var existingChannels = await _analyticsService.GetAllChannelsAsync(includeInactive: true);

        if (envelope?.Channels is { Count: > 0 } centralChannels)
        {
            foreach (var cc in centralChannels.Where(c => c.IsEnabled && c.ShowInAnalytics))
            {
                var platform = Enum.TryParse<SocialPlatform>(cc.Platform, true, out var parsedPlat)
                    ? parsedPlat
                    : SocialPlatform.YouTube;

                var targetId = $"central-{SlugKey(cc.Key)}";
                var existing = existingChannels.FirstOrDefault(x =>
                    x.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase) ||
                    ((x.IsAdminOwned || string.IsNullOrWhiteSpace(x.OwnerUserId)) &&
                     !string.IsNullOrWhiteSpace(cc.Handle) &&
                     x.ChannelHandle.Equals(cc.Handle, StringComparison.OrdinalIgnoreCase)));

                var channel = existing ?? new ChannelMetrics { Id = targetId, RpmUsd = 3.20 };
                channel.ChannelName = !string.IsNullOrWhiteSpace(cc.Name) ? cc.Name : channel.ChannelName;
                channel.ChannelHandle = !string.IsNullOrWhiteSpace(cc.Handle) ? cc.Handle : channel.ChannelHandle;
                channel.Platform = platform;
                channel.Niche = !string.IsNullOrWhiteSpace(cc.Niche) ? cc.Niche : channel.Niche;
                channel.ChannelUrl = !string.IsNullOrWhiteSpace(cc.Url) ? cc.Url : channel.ChannelUrl;
                channel.OwnerUserId = string.IsNullOrWhiteSpace(channel.OwnerUserId) ? "admin" : channel.OwnerUserId;
                channel.IsAdminOwned = true;
                if (!string.IsNullOrWhiteSpace(cc.ChannelId))
                    channel.ExternalChannelId = cc.ChannelId;
                if (string.IsNullOrWhiteSpace(channel.DataSource) || channel.DataSource == "Admin")
                    channel.DataSource = "CentralConfig";
                channel.IsActive = true;

                if (platform == SocialPlatform.YouTube)
                {
                    if (await _youtubeFetch.EnrichChannelAsync(channel, apiKey, ct))
                    {
                        summary.LiveVideosFetched += channel.RecentVideos.Count;
                    }
                }

                await _analyticsService.UpsertChannelAsync(channel);
                if (existing is null) summary.ImportedChannels++;
                else summary.UpdatedChannels++;
            }
        }

        // Also create zero-click initial funnels & opportunities from live ecosystem websites if none exist
        if (envelope?.Websites is { Count: > 0 } sites)
        {
            var existingCampaigns = await _funnelService.GetCampaignsAsync(activeOnly: false);
            var existingTargets = existingCampaigns
                .Select(c => c.DestinationUrl.TrimEnd('/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var w in sites.Where(w =>
                w.ShowInEcosystemBar &&
                !w.Key.Equals("social", StringComparison.OrdinalIgnoreCase) &&
                !w.Key.Equals("admin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(w.Url)))
            {
                var cleanUrl = w.Url.TrimEnd('/');
                if (!existingTargets.Contains(cleanUrl))
                {
                    await _funnelService.UpsertCampaignAsync(new FunnelCampaign
                    {
                        Id = $"f-{SlugKey(w.Key)}",
                        Title = $"{w.Name} Ecosystem Funnel",
                        SourcePlatform = "YouTube Description",
                        DestinationUrl = w.Url,
                        TrackingSlug = $"go-{SlugKey(w.Key)}",
                        TotalClicks = 0,
                        Conversions = 0,
                        OwnerUserId = "admin",
                        IsAdminOwned = true,
                        IsActive = true
                    });
                    existingTargets.Add(cleanUrl);
                    summary.FunnelsCreated++;
                }
            }

            // Update default destination URL if currently blank
            if (string.IsNullOrWhiteSpace(site.DefaultDestinationBaseUrl))
            {
                var studio = sites.FirstOrDefault(w => w.Key.Equals("studio", StringComparison.OrdinalIgnoreCase)) ?? sites.FirstOrDefault();
                if (studio is not null)
                {
                    site.DefaultDestinationBaseUrl = studio.Url;
                    await _configService.SaveSiteConfigAsync(site);
                }
            }
        }

        summary.Message = $"Synced {summary.ImportedChannels} new + {summary.UpdatedChannels} updated channels ({summary.LiveVideosFetched} live videos), created {summary.FunnelsCreated} zero-click ecosystem funnels.";
        return summary;
    }

    public async Task<EcosystemSyncSummary> RefreshAllYouTubeLiveAsync(CancellationToken ct = default)
    {
        var summary = new EcosystemSyncSummary { SyncedAt = DateTime.UtcNow };
        var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
        summary.UsedYouTubeApiKey = !string.IsNullOrWhiteSpace(apiKey);

        var channels = await _analyticsService.GetAllChannelsAsync(includeInactive: true);
        foreach (var channel in channels.Where(c =>
            c.Platform == SocialPlatform.YouTube &&
            (c.IsAdminOwned || string.IsNullOrWhiteSpace(c.OwnerUserId))))
        {
            if (await _youtubeFetch.EnrichChannelAsync(channel, apiKey, ct))
            {
                await _analyticsService.UpsertChannelAsync(channel);
                summary.UpdatedChannels++;
                summary.LiveVideosFetched += channel.RecentVideos.Count;
            }
        }

        summary.Message = $"Refreshed {summary.UpdatedChannels} YouTube channel(s) and loaded {summary.LiveVideosFetched} real recent videos.";
        return summary;
    }

    private static string SlugKey(string raw)
    {
        var chars = raw.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
