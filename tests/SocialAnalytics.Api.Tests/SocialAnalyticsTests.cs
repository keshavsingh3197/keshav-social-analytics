using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SocialAnalytics.Api.Controllers;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;
using Xunit;

namespace SocialAnalytics.Api.Tests;

public sealed class SocialAnalyticsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"social-tests-{Guid.NewGuid():N}");

    public SocialAnalyticsTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private HybridDataStore CreateStore()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var env = new FakeHostEnv(_tempDir);
        return new HybridDataStore(cfg, env, NullLogger<HybridDataStore>.Instance);
    }

    [Fact]
    public void SeedData_ContainsZeroFakeChannelsOrFakeClickCampaigns()
    {
        Assert.Empty(SocialSeedData.DefaultChannels());
        Assert.Empty(SocialSeedData.DefaultCampaigns());
        Assert.DoesNotContain("@KeshavSingh", SocialSeedData.DefaultSiteConfig().HeroSubtitle);
    }

    [Fact]
    public void SocialAdminController_StrictlyRequiresAdminRole()
    {
        var authAttr = typeof(SocialAdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal("Admin", authAttr!.Roles);
    }

    [Theory]
    [InlineData("1.24K subscribers", 1240)]
    [InlineData("45.2K", 45200)]
    [InlineData("1.3M subscribers", 1300000)]
    [InlineData("920 subscribers", 920)]
    public void ParseCompactNumber_ParsesYouTubeCounts(string raw, long expected)
    {
        Assert.Equal(expected, YouTubeLiveFetchService.ParseCompactNumber(raw));
    }

    [Fact]
    public async Task YouTubeLiveFetch_ResolvesHandleAndParsesAtomFeed()
    {
        var publishedRecent = DateTime.UtcNow.AddDays(-2).ToString("O");
        var handler = new StubHttpHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("youtube.com/@keshavsinghworld"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        <html>
                        <meta property="og:title" content="Keshav Singh World">
                        <meta property="og:image" content="https://yt3.example/avatar.jpg">
                        <script>{"externalId":"UC1234567890123456789012","subscriberCountText":{"simpleText":"1.25K subscribers"}}</script>
                        </html>
                        """, Encoding.UTF8, "text/html")
                };
            }
            if (url.Contains("feeds/videos.xml?channel_id=UC1234567890123456789012"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"""
                        <?xml version="1.0" encoding="UTF-8"?>
                        <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015"
                              xmlns:media="http://search.yahoo.com/mrss/"
                              xmlns="http://www.w3.org/2005/Atom">
                          <title>Keshav Singh World</title>
                          <entry>
                            <yt:videoId>realVid123</yt:videoId>
                            <title>Live Architecture Walkthrough</title>
                            <link rel="alternate" href="https://www.youtube.com/watch?v=realVid123"/>
                            <published>{publishedRecent}</published>
                            <media:group>
                              <media:description>Demo walkthrough</media:description>
                              <media:thumbnail url="https://i.ytimg.com/vi/realVid123/hqdefault.jpg"/>
                              <media:community><media:statistics views="4200"/></media:community>
                            </media:group>
                          </entry>
                        </feed>
                        """, Encoding.UTF8, "application/xml")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var svc = new YouTubeLiveFetchService(new StubHttpClientFactory(handler), NullLogger<YouTubeLiveFetchService>.Instance);
        var channel = new ChannelMetrics
        {
            ChannelHandle = "@keshavsinghworld",
            ChannelUrl = "https://www.youtube.com/@keshavsinghworld",
            Platform = SocialPlatform.YouTube,
            RpmUsd = 3.5
        };

        var ok = await svc.EnrichChannelAsync(channel, apiKey: null);
        Assert.True(ok);
        Assert.Equal("UC1234567890123456789012", channel.ExternalChannelId);
        Assert.Equal(1250, channel.FollowersOrSubscribers);
        Assert.Equal(4200, channel.TotalViewsLast30Days);
        Assert.Equal("YouTubeRSS", channel.DataSource);
        Assert.True(channel.IsVerifiedLive);
        var vid = Assert.Single(channel.RecentVideos);
        Assert.Equal("realVid123", vid.VideoId);
        Assert.Equal(4200, vid.Views);
    }

    [Fact]
    public async Task CentralSync_ImportsRealChannelsAndPurgesLegacyHandles()
    {
        var store = CreateStore();
        var cfgService = new SocialConfigService(store);
        var analytics = new AnalyticsEngineService(store, cfgService);
        var funnels = new FunnelService(store);

        // Seed a legacy demo row that must be purged
        await analytics.UpsertChannelAsync(new ChannelMetrics
        {
            Id = "ch-1",
            ChannelName = "Old Static",
            ChannelHandle = "@KeshavSingh",
            Platform = SocialPlatform.YouTube
        });

        var publishedRecent = DateTime.UtcNow.AddDays(-1).ToString("O");
        var handler = new StubHttpHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.EndsWith("/api/config"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "websites": [
                            { "key": "studio", "name": "Studio AI", "url": "https://studio.keshavsingh.in", "showInEcosystemBar": true, "sortOrder": 10 }
                          ],
                          "channels": [
                            { "key": "omkar", "name": "Omkar Shiv Bhakti", "handle": "@OmkarShivBhakti", "platform": "YouTube", "url": "https://www.youtube.com/@OmkarShivBhakti", "niche": "Devotional", "showInAnalytics": true, "isEnabled": true }
                          ]
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }
            if (url.Contains("@OmkarShivBhakti"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""<html><script>{"externalId":"UC9999999999999999999999"}</script></html>""")
                };
            }
            if (url.Contains("UC9999999999999999999999"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"""
                        <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
                          <entry><yt:videoId>omkrReal1</yt:videoId><title>Shiv Bhakti Katha</title><published>{publishedRecent}</published><media:group><media:community><media:statistics views="1500"/></media:community></media:group></entry>
                        </feed>
                        """)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var factory = new StubHttpClientFactory(handler);
        var yt = new YouTubeLiveFetchService(factory, NullLogger<YouTubeLiveFetchService>.Instance);
        var cfg = new ConfigurationBuilder().Build();
        var sync = new CentralEcosystemSyncService(factory, cfg, cfgService, analytics, funnels, yt, NullLogger<CentralEcosystemSyncService>.Instance);

        var summary = await sync.SyncAllAsync(purgeLegacy: true);
        Assert.Equal(1, summary.PurgedLegacyRecords);
        Assert.Equal(1, summary.ImportedChannels);
        Assert.Equal(1, summary.LiveVideosFetched);
        Assert.Equal(1, summary.FunnelsCreated);

        var channels = await analytics.GetAllChannelsAsync();
        var only = Assert.Single(channels);
        Assert.Equal("@OmkarShivBhakti", only.ChannelHandle);
        Assert.Equal(1500, only.TotalViewsLast30Days);

        var recentVids = await analytics.GetRecentVideosAsync();
        Assert.Single(recentVids);
    }

    [Fact]
    public async Task IntegrationsView_MasksYouTubeApiKey()
    {
        var cfgService = new SocialConfigService(CreateStore());
        var view = await cfgService.UpdateIntegrationSettingsAsync(new UpdateIntegrationSettingsRequest
        {
            YouTubeApiKey = "AIzaSyExampleSecretKey1234"
        });

        Assert.True(view.HasYouTubeApiKey);
        Assert.Equal("AIza••••••••1234", view.YouTubeApiKeyMasked);
        Assert.DoesNotContain("ExampleSecret", view.YouTubeApiKeyMasked);
    }

    private sealed class FakeHostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SocialAnalytics.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
