namespace SocialAnalytics.Api.Services;

/// <summary>
/// Non-blocking startup warmup that replaces any legacy demo channels with central
/// ecosystem channels (<c>@keshavsinghworld</c>, <c>@OmkarShivBhakti</c>) and hydrates
/// real public YouTube feed statistics without blocking Kestrel startup.
/// </summary>
public sealed class EcosystemWarmupService : BackgroundService
{
    private readonly CentralEcosystemSyncService _syncService;
    private readonly AnalyticsEngineService _analyticsService;
    private readonly ILogger<EcosystemWarmupService> _logger;

    public EcosystemWarmupService(
        CentralEcosystemSyncService syncService,
        AnalyticsEngineService analyticsService,
        ILogger<EcosystemWarmupService> logger)
    {
        _syncService = syncService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        try
        {
            var existing = await _analyticsService.GetAllChannelsAsync(includeInactive: true);
            var onlyLegacyOrEmpty = existing.Count == 0 ||
                existing.All(c => SocialSeedData.LegacyDemoChannelIds.Contains(c.Id) ||
                                  SocialSeedData.LegacyDemoHandles.Contains(c.ChannelHandle));

            if (onlyLegacyOrEmpty)
            {
                await _syncService.SyncAllAsync(purgeLegacy: true, stoppingToken);
            }
            else if (existing.Any(c => c.Platform == Models.SocialPlatform.YouTube && !c.IsVerifiedLive))
            {
                await _syncService.RefreshAllYouTubeLiveAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background ecosystem/YouTube warmup could not complete (offline or central config unreachable).");
        }
    }
}
