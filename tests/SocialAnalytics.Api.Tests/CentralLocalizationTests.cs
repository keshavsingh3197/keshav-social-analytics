using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SocialAnalytics.Api.Services;
using Xunit;

namespace SocialAnalytics.Api.Tests;

public sealed class CentralLocalizationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"social-i18n-{Guid.NewGuid():N}");

    public CentralLocalizationTests() => Directory.CreateDirectory(_tempDir);

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
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task SocialLocalization_DefaultsToEnglish_AndServesBilingualBundles()
    {
        var cfg = new ConfigurationBuilder().Build();
        var store = new HybridDataStore(cfg, new TestHostEnv(_tempDir), NullLogger<HybridDataStore>.Instance);
        var service = new CentralLocalizationService(store, cfg, NullLogger<CentralLocalizationService>.Instance);

        var manifest = await service.GetManifestAsync();
        Assert.Equal("en", manifest.Policy.DefaultLocale);
        Assert.Equal("admin", manifest.Policy.ControlledBy);
        Assert.Contains("en", manifest.Policy.EnabledLocales);
        Assert.Contains("hi", manifest.Policy.EnabledLocales);

        var enBundle = await service.GetBundleAsync(null);
        Assert.Equal("en", enBundle.Locale);
        Assert.Equal("Social Channel Performance & Growth Hub", enBundle.Entries["social.hero.title"]);

        var hiBundle = await service.GetBundleAsync("hi");
        Assert.Equal("hi", hiBundle.Locale);
        Assert.Equal("सोशल चैनल प्रदर्शन और ग्रोथ हब", hiBundle.Entries["social.hero.title"]);
    }

    [Fact]
    public async Task SocialLocalization_PerSiteOverride_ForcesHindiLocale()
    {
        var cfg = new ConfigurationBuilder().Build();
        var store = new HybridDataStore(cfg, new TestHostEnv(_tempDir), NullLogger<HybridDataStore>.Instance);
        var service = new CentralLocalizationService(store, cfg, NullLogger<CentralLocalizationService>.Instance);

        var state = await service.UpdateLocalSettingsAsync(
            new UpdateSiteLocalizationRequest(
                OverrideCentralLocalization: true,
                DefaultLocale: "hi",
                EnabledLocales: ["hi"],
                AllowVisitorLanguageSwitch: false,
                ShowLanguagePicker: false,
                ForceLocale: true),
            "admin-user");

        Assert.Equal("site-override", state.EffectivePolicy.ControlledBy);
        Assert.Equal("hi", state.EffectivePolicy.DefaultLocale);
        Assert.True(state.EffectivePolicy.ForceLocale);

        var bundle = await service.GetBundleAsync("en");
        Assert.Equal("hi", bundle.Locale);
        Assert.Equal("सोशल चैनल प्रदर्शन और ग्रोथ हब", bundle.Entries["social.hero.title"]);
    }

    [Fact]
    public async Task TryLoadCentralEnvelopeFromMongoAsync_ReturnsNull_WhenUnconfigured()
    {
        var cfg = new ConfigurationBuilder().Build();
        var store = new HybridDataStore(cfg, new TestHostEnv(_tempDir), NullLogger<HybridDataStore>.Instance);
        var service = new CentralLocalizationService(store, cfg, NullLogger<CentralLocalizationService>.Instance);

        var envelope = await service.TryLoadCentralEnvelopeFromMongoAsync();
        Assert.Null(envelope);
    }
}
