using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public sealed class SocialConfigService
{
    private const string EarningsCollection = "earnings_config";
    private const string SiteCollection = "site_config";

    private readonly HybridDataStore _store;

    public SocialConfigService(HybridDataStore store)
    {
        _store = store;
    }

    public string StorageMode => _store.StorageMode;

    public Task<EarningsConfig> GetEarningsConfigAsync() =>
        _store.GetSingletonAsync(EarningsCollection, "default", SocialSeedData.DefaultEarningsConfig);

    public Task<EarningsConfig> SaveEarningsConfigAsync(EarningsConfig config)
    {
        config.Id = "default";
        config.UpdatedAt = DateTime.UtcNow;
        config.PlatformBenchmarks ??= [];
        config.Niches ??= [];
        config.Opportunities ??= [];
        return _store.SaveSingletonAsync(EarningsCollection, "default", config);
    }

    public Task<SiteConfig> GetSiteConfigAsync() =>
        _store.GetSingletonAsync(SiteCollection, "default", SocialSeedData.DefaultSiteConfig);

    public Task<SiteConfig> SaveSiteConfigAsync(SiteConfig config)
    {
        config.Id = "default";
        config.UpdatedAt = DateTime.UtcNow;
        return _store.SaveSingletonAsync(SiteCollection, "default", config);
    }
}
