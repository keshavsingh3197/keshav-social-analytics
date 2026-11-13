using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SocialAnalytics.Api.Controllers;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;
using Xunit;

namespace SocialAnalytics.Api.Tests;

public sealed class CentralAdminIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"social-central-{Guid.NewGuid():N}");

    public CentralAdminIntegrationTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class TestHostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SocialAnalytics.Api.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }

    private HybridDataStore CreateStore()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        return new HybridDataStore(cfg, new TestHostEnv(_tempDir), NullLogger<HybridDataStore>.Instance);
    }

    private static string EncryptLikeDataProtector(string plaintext, byte[] key, byte keyId, bool includeVersionHeader = true)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var header = includeVersionHeader ? 2 : 0;
        var payload = new byte[header + 12 + 16 + cipher.Length];
        if (includeVersionHeader)
        {
            payload[0] = 1;
            payload[1] = keyId;
        }
        Buffer.BlockCopy(nonce, 0, payload, header, 12);
        Buffer.BlockCopy(tag, 0, payload, header + 12, 16);
        Buffer.BlockCopy(cipher, 0, payload, header + 28, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    [Fact]
    public void DecryptProtectedValue_SupportsVersionedRotatedAndLegacyCiphertexts()
    {
        var retiredKey = Enumerable.Range(10, 32).Select(i => (byte)i).ToArray();
        var currentKey = Enumerable.Range(50, 32).Select(i => (byte)i).ToArray();

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Encryption:DataKey"] = Convert.ToBase64String(currentKey),
            ["Encryption:DataKeyId"] = "2",
            ["Encryption:PreviousDataKeys:0:KeyId"] = "1",
            ["Encryption:PreviousDataKeys:0:Key"] = Convert.ToBase64String(retiredKey)
        }).Build();

        var keys = CentralAdminConfigReader.LoadEncryptionKeys(cfg, out var activeId);
        Assert.Equal((byte)2, activeId);

        var versionedCurrent = EncryptLikeDataProtector("AIzaSyCentralKey9999", currentKey, 2, true);
        var versionedRetired = EncryptLikeDataProtector("GOCSPX-rotated-secret", retiredKey, 1, true);
        var legacyUnheadered = EncryptLikeDataProtector("legacy-yt-key-1111", currentKey, 2, false);

        Assert.Equal("AIzaSyCentralKey9999", CentralAdminConfigReader.DecryptProtectedValue(versionedCurrent, keys, activeId));
        Assert.Equal("GOCSPX-rotated-secret", CentralAdminConfigReader.DecryptProtectedValue(versionedRetired, keys, activeId));
        Assert.Equal("legacy-yt-key-1111", CentralAdminConfigReader.DecryptProtectedValue(legacyUnheadered, keys, activeId));
    }

    [Fact]
    public async Task SocialConfigService_PrefersCentralAdminDbCredentialsAndHydratesAdSense()
    {
        var store = CreateStore();
        await store.SaveSingletonAsync("site_config", "default", new SiteConfig
        {
            Ads = new AdMonetizationConfig
            {
                AdsEnabled = true,
                HeaderAdSlotId = "1234567890",
                AdSenseClientId = ""
            }
        });

        var reader = new CentralAdminConfigReader(
            _ => Task.FromResult<CentralAdminIntegrationsSnapshot?>(new CentralAdminIntegrationsSnapshot(
                YouTubeApiKey: "AIzaSyCentralYT1234",
                GoogleOAuthClientId: "central-oauth.apps.googleusercontent.com",
                GoogleOAuthClientSecret: "GOCSPX-central-5678",
                AdSenseClientId: "ca-pub-9988776655443322")),
            NullLogger<CentralAdminConfigReader>.Instance);

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["YouTube:ApiKey"] = "AIzaSyLocalFallback",
            ["YouTube:OAuth:RedirectUri"] = "https://social.keshavsingh.in/youtube/callback"
        }).Build();

        var service = new SocialConfigService(store, cfg, reader);

        Assert.Equal("AIzaSyCentralYT1234", await service.GetEffectiveYouTubeApiKeyAsync());

        var oauth = await service.GetEffectiveGoogleOAuthAsync();
        Assert.Equal("central-oauth.apps.googleusercontent.com", oauth.ClientId);
        Assert.Equal("GOCSPX-central-5678", oauth.ClientSecret);
        Assert.Equal("https://social.keshavsingh.in/youtube/callback", oauth.RedirectUri);

        var site = await service.GetSiteConfigAsync();
        Assert.Equal("ca-pub-9988776655443322", site.Ads.AdSenseClientId);
        Assert.Equal("1234567890", site.Ads.HeaderAdSlotId);

        var view = await service.GetIntegrationSettingsViewAsync();
        Assert.True(view.ManagedByCentralAdmin);
        Assert.True(view.CentralYouTubeApiKeyConfigured);
        Assert.True(view.CentralGoogleOAuthConfigured);
        Assert.True(view.CentralAdSenseConfigured);
        Assert.Equal("••••1234", view.YouTubeApiKeyMasked);
        Assert.Equal("••••5678", view.GoogleClientSecretMasked);
        Assert.Equal("ca-pub-9988776655443322", view.AdSenseClientId);
    }

    [Fact]
    public void DuplicateHealthController_IsRemovedSoMinimalHealthEndpointIsUnambiguous()
    {
        var apiAssembly = typeof(SocialAdminController).Assembly;
        var healthControllers = apiAssembly.GetTypes()
            .Where(t => string.Equals(t.Name, "HealthController", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(healthControllers);
    }
}
