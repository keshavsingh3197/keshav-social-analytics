using System.Text.RegularExpressions;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public sealed partial class SocialConfigService
{
    private const string EarningsCollection = "earnings_config";
    private const string SiteCollection = "site_config";
    private const string SecretsCollection = "integration_secrets";

    private readonly HybridDataStore _store;
    private readonly IConfiguration _configuration;
    private readonly CentralAdminConfigReader? _centralReader;

    public SocialConfigService(
        HybridDataStore store,
        IConfiguration? configuration = null,
        CentralAdminConfigReader? centralReader = null)
    {
        _store = store;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _centralReader = centralReader;
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

    public async Task<SiteConfig> GetSiteConfigAsync()
    {
        var site = await _store.GetSingletonAsync(SiteCollection, "default", SocialSeedData.DefaultSiteConfig);
        site.Ads ??= new AdMonetizationConfig();

        if (_centralReader is not null)
        {
            var central = await _centralReader.GetSnapshotAsync();
            if (!string.IsNullOrWhiteSpace(central.AdSenseClientId) &&
                AdSenseClientRegex().IsMatch(central.AdSenseClientId.Trim()))
            {
                site.Ads.AdSenseClientId = central.AdSenseClientId.Trim();
            }
        }

        return site;
    }

    public async Task<SiteConfig> SaveSiteConfigAsync(SiteConfig config)
    {
        config.Id = "default";
        config.UpdatedAt = DateTime.UtcNow;
        config.Ads = SanitizeAdConfig(config.Ads);
        await _store.SaveSingletonAsync(SiteCollection, "default", config);
        return await GetSiteConfigAsync();
    }

    public Task<IntegrationSecrets> GetSecretsAsync() =>
        _store.GetSingletonAsync(SecretsCollection, "default", () => new IntegrationSecrets());

    public async Task<string?> GetEffectiveYouTubeApiKeyAsync()
    {
        var central = _centralReader is not null ? await _centralReader.GetSnapshotAsync() : null;
        var secrets = await GetSecretsAsync();
        return FirstNonEmpty(
            central?.YouTubeApiKey,
            secrets.YouTubeApiKey,
            _configuration["YouTube:ApiKey"],
            _configuration["YouTube:Data:ApiKey"],
            Environment.GetEnvironmentVariable("YOUTUBE_API_KEY"));
    }

    public async Task<(string? ClientId, string? ClientSecret, string? RedirectUri)> GetEffectiveGoogleOAuthAsync()
    {
        var central = _centralReader is not null ? await _centralReader.GetSnapshotAsync() : null;
        var secrets = await GetSecretsAsync();
        var clientId = FirstNonEmpty(
            central?.GoogleOAuthClientId,
            secrets.GoogleClientId,
            _configuration["YouTube:OAuth:ClientId"],
            _configuration["YouTube:Publish:ClientId"],
            _configuration["Google:ClientId"],
            Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID"),
            Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"));

        var clientSecret = FirstNonEmpty(
            central?.GoogleOAuthClientSecret,
            secrets.GoogleClientSecret,
            _configuration["YouTube:OAuth:ClientSecret"],
            _configuration["YouTube:Publish:ClientSecret"],
            _configuration["Google:ClientSecret"],
            Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET"),
            Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET"));

        var redirectUri = FirstNonEmpty(
            secrets.OAuthRedirectUri,
            _configuration["YouTube:OAuth:RedirectUri"],
            _configuration["YouTube:Publish:RedirectUri"],
            Environment.GetEnvironmentVariable("YOUTUBE_REDIRECT_URI"));

        return (clientId, clientSecret, redirectUri);
    }

    public async Task<IntegrationSettingsView> GetIntegrationSettingsViewAsync()
    {
        var site = await GetSiteConfigAsync();
        var secrets = await GetSecretsAsync();
        var central = _centralReader is not null ? await _centralReader.GetSnapshotAsync() : null;
        var apiKey = await GetEffectiveYouTubeApiKeyAsync();
        var (clientId, clientSecret, redirectUri) = await GetEffectiveGoogleOAuthAsync();

        var centralYtConfigured = !string.IsNullOrWhiteSpace(central?.YouTubeApiKey);
        var centralOAuthConfigured = !string.IsNullOrWhiteSpace(central?.GoogleOAuthClientId) &&
                                     !string.IsNullOrWhiteSpace(central?.GoogleOAuthClientSecret);
        var centralAdSenseConfigured = !string.IsNullOrWhiteSpace(central?.AdSenseClientId) &&
                                       AdSenseClientRegex().IsMatch(central.AdSenseClientId.Trim());

        return new IntegrationSettingsView
        {
            HasYouTubeApiKey = !string.IsNullOrWhiteSpace(apiKey),
            YouTubeApiKeyMasked = MaskSecret(apiKey),
            HasGoogleClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
            GoogleClientSecretMasked = MaskSecret(clientSecret),
            GoogleClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
            OAuthRedirectUri = string.IsNullOrWhiteSpace(redirectUri) ? null : redirectUri,
            IsYouTubeOAuthConfigured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret),
            ManagedByCentralAdmin = centralYtConfigured || centralOAuthConfigured || centralAdSenseConfigured || (_centralReader?.IsConfigured ?? false),
            CentralYouTubeApiKeyConfigured = centralYtConfigured,
            CentralGoogleOAuthConfigured = centralOAuthConfigured,
            CentralAdSenseConfigured = centralAdSenseConfigured,
            AdSenseClientId = string.IsNullOrWhiteSpace(site.Ads?.AdSenseClientId) ? null : site.Ads.AdSenseClientId,
            EcosystemConfigUrl = site.EcosystemConfigUrl,
            AdminPortalUrl = site.AdminPortalUrl,
            UpdatedAt = secrets.UpdatedAt > site.UpdatedAt ? secrets.UpdatedAt : site.UpdatedAt
        };
    }

    public async Task<IntegrationSettingsView> UpdateIntegrationSettingsAsync(UpdateIntegrationSettingsRequest req)
    {
        var secrets = await GetSecretsAsync();
        var site = await GetSiteConfigAsync();
        var secretsChanged = false;

        if (req.ClearYouTubeApiKey)
        {
            secrets.YouTubeApiKey = null;
            secretsChanged = true;
        }
        else if (!string.IsNullOrWhiteSpace(req.YouTubeApiKey) && !req.YouTubeApiKey.Contains("••••", StringComparison.Ordinal))
        {
            secrets.YouTubeApiKey = req.YouTubeApiKey.Trim();
            secretsChanged = true;
        }

        if (req.ClearGoogleOAuth)
        {
            secrets.GoogleClientId = null;
            secrets.GoogleClientSecret = null;
            secrets.OAuthRedirectUri = null;
            secretsChanged = true;
        }
        else
        {
            if (req.GoogleClientId is not null)
            {
                var cid = req.GoogleClientId.Trim();
                secrets.GoogleClientId = string.IsNullOrEmpty(cid) ? null : cid;
                secretsChanged = true;
            }
            if (!string.IsNullOrWhiteSpace(req.GoogleClientSecret) && !req.GoogleClientSecret.Contains("••••", StringComparison.Ordinal))
            {
                secrets.GoogleClientSecret = req.GoogleClientSecret.Trim();
                secretsChanged = true;
            }
            if (req.OAuthRedirectUri is not null)
            {
                var ruri = req.OAuthRedirectUri.Trim();
                secrets.OAuthRedirectUri = string.IsNullOrEmpty(ruri) ? null : ruri;
                secretsChanged = true;
            }
        }

        if (secretsChanged)
        {
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

    public static AdMonetizationConfig SanitizeAdConfig(AdMonetizationConfig? input)
    {
        if (input is null) return new AdMonetizationConfig();

        var client = input.AdSenseClientId?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(client) && !AdSenseClientRegex().IsMatch(client))
            client = string.Empty;

        static string SanitizeSlot(string? raw)
        {
            var s = raw?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(s) && AdSlotRegex().IsMatch(s) ? s : string.Empty;
        }

        var sponsorUrl = input.SponsorUrl?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(sponsorUrl) &&
            (!Uri.TryCreate(sponsorUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            sponsorUrl = string.Empty;
        }

        return new AdMonetizationConfig
        {
            AdsEnabled = input.AdsEnabled,
            AdSenseClientId = client,
            HeaderAdSlotId = SanitizeSlot(input.HeaderAdSlotId),
            InFeedAdSlotId = SanitizeSlot(input.InFeedAdSlotId),
            SidebarAdSlotId = SanitizeSlot(input.SidebarAdSlotId),
            FooterAdSlotId = SanitizeSlot(input.FooterAdSlotId),
            AdLabel = string.IsNullOrWhiteSpace(input.AdLabel) ? "Advertisement" : input.AdLabel.Trim(),
            SponsorBannerEnabled = input.SponsorBannerEnabled,
            SponsorBadge = string.IsNullOrWhiteSpace(input.SponsorBadge) ? "Featured" : input.SponsorBadge.Trim(),
            SponsorTitle = input.SponsorTitle?.Trim() ?? string.Empty,
            SponsorDescription = input.SponsorDescription?.Trim() ?? string.Empty,
            SponsorCtaText = string.IsNullOrWhiteSpace(input.SponsorCtaText) ? "Learn More" : input.SponsorCtaText.Trim(),
            SponsorUrl = sponsorUrl
        };
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim();

    internal static string? MaskSecret(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var clean = key.Trim();
        return clean.Length <= 4
            ? "••••"
            : $"••••{clean[^4..]}";
    }

    [GeneratedRegex(@"^ca-pub-\d{10,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex AdSenseClientRegex();

    [GeneratedRegex(@"^\d{6,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex AdSlotRegex();
}
