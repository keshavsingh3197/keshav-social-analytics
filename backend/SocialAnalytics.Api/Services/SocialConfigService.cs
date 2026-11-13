using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public sealed class SocialConfigService
{
    private const string EarningsCollection = "earnings_config";
    private const string SiteCollection = "site_config";
    private const string SecretsCollection = "integration_secrets";

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

    public Task<IntegrationSecrets> GetSecretsAsync() =>
        _store.GetSingletonAsync(SecretsCollection, "default", () => new IntegrationSecrets());

    public async Task<IntegrationSettingsView> GetIntegrationSettingsViewAsync()
    {
        var site = await GetSiteConfigAsync();
        var secrets = await GetSecretsAsync();
        var key = secrets.YouTubeApiKey?.Trim();
        return new IntegrationSettingsView
        {
            HasYouTubeApiKey = !string.IsNullOrWhiteSpace(key),
            YouTubeApiKeyMasked = string.IsNullOrWhiteSpace(key)
                ? null
                : key.Length <= 8
                    ? "••••••••"
                    : $"{key[..4]}••••••••{key[^4..]}",
            EcosystemConfigUrl = site.EcosystemConfigUrl,
            AdminPortalUrl = site.AdminPortalUrl,
            UpdatedAt = secrets.UpdatedAt > site.UpdatedAt ? secrets.UpdatedAt : site.UpdatedAt
        };
    }

    public async Task<IntegrationSettingsView> UpdateIntegrationSettingsAsync(UpdateIntegrationSettingsRequest req)
    {
        var secrets = await GetSecretsAsync();
        var site = await GetSiteConfigAsync();

        if (req.ClearYouTubeApiKey)
        {
            secrets.YouTubeApiKey = null;
            secrets.UpdatedAt = DateTime.UtcNow;
            await _store.SaveSingletonAsync(SecretsCollection, "default", secrets);
        }
        else if (!string.IsNullOrWhiteSpace(req.YouTubeApiKey))
        {
            secrets.YouTubeApiKey = req.YouTubeApiKey.Trim();
            secrets.UpdatedAt = DateTime.UtcNow;
            await _store.SaveSingletonAsync(SecretsCollection, "default", secrets);
        }

        var siteChanged = false;
        if (!string.IsNullOrWhiteSpace(req.EcosystemConfigUrl))
        {
            site.EcosystemConfigUrl = req.EcosystemConfigUrl.Trim();
            siteChanged = true;
        }
        if (!string.IsNullOrWhiteSpace(req.AdminPortalUrl))
        {
            site.AdminPortalUrl = req.AdminPortalUrl.Trim().TrimEnd('/');
            siteChanged = true;
        }
        if (siteChanged)
        {
            await SaveSiteConfigAsync(site);
        }

        return await GetIntegrationSettingsViewAsync();
    }
}
