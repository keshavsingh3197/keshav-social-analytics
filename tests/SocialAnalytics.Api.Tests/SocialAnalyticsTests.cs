using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public async Task IntegrationsView_MasksYouTubeApiKeyAndGoogleClientSecret()
    {
        var cfgService = new SocialConfigService(CreateStore());
        var view = await cfgService.UpdateIntegrationSettingsAsync(new UpdateIntegrationSettingsRequest
        {
            YouTubeApiKey = "AIzaSyExampleSecretKey1234",
            GoogleClientId = "123456.apps.googleusercontent.com",
            GoogleClientSecret = "GOCSPX-MyVerySecretValue9999",
            OAuthRedirectUri = "https://social.keshavsingh.in/youtube/callback"
        });

        Assert.True(view.HasYouTubeApiKey);
        Assert.Equal("••••1234", view.YouTubeApiKeyMasked);
        Assert.DoesNotContain("AIza", view.YouTubeApiKeyMasked);
        Assert.DoesNotContain("ExampleSecret", view.YouTubeApiKeyMasked);

        Assert.True(view.HasGoogleClientSecret);
        Assert.True(view.IsYouTubeOAuthConfigured);
        Assert.Equal("123456.apps.googleusercontent.com", view.GoogleClientId);
        Assert.Equal("••••9999", view.GoogleClientSecretMasked);
        Assert.DoesNotContain("GOCS", view.GoogleClientSecretMasked);
        Assert.DoesNotContain("MyVerySecret", view.GoogleClientSecretMasked);
    }

    [Fact]
    public async Task PrivacyIsolation_AnonymousAndOtherUsersNeverSeeAdminOrPeerChannelsOrFunnels()
    {
        var store = CreateStore();
        var cfgService = new SocialConfigService(store);
        var analytics = new AnalyticsEngineService(store, cfgService);
        var funnels = new FunnelService(store);
        var yt = new YouTubeLiveFetchService(new StubHttpClientFactory(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), NullLogger<YouTubeLiveFetchService>.Instance);
        var rev = new RevenueCalculatorService(cfgService);
        var adv = new GrowthAdvisorService(store, analytics);
        var lookup = new ChannelLookupService(cfgService, yt, rev, adv);

        // Admin-owned synced channels & funnels
        await analytics.UpsertChannelAsync(new ChannelMetrics
        {
            Id = "central-keshav",
            ChannelName = "Keshav Singh World",
            ChannelHandle = "@keshavsinghworld",
            Platform = SocialPlatform.YouTube,
            FollowersOrSubscribers = 50000,
            TotalViewsLast30Days = 400000,
            OwnerUserId = "admin",
            IsAdminOwned = true,
            IsActive = true
        });
        await funnels.UpsertCampaignAsync(new FunnelCampaign
        {
            Id = "f-admin",
            Title = "Admin Private Funnel",
            TrackingSlug = "go-studio",
            DestinationUrl = "https://studio.keshavsingh.in",
            OwnerUserId = "admin",
            IsAdminOwned = true,
            TotalClicks = 99
        });

        // User A creates their own channel & funnel
        var userAChan = await analytics.UpsertUserChannelAsync("user-a", isAdmin: false, new ChannelMetrics
        {
            ChannelName = "User A Creator",
            ChannelHandle = "@useracreator",
            Platform = SocialPlatform.YouTube,
            FollowersOrSubscribers = 1500,
            TotalViewsLast30Days = 20000
        });
        Assert.NotNull(userAChan);
        var userAFunnel = await funnels.UpsertUserCampaignAsync("user-a", isAdmin: false, new FunnelCampaign
        {
            Title = "User A Link",
            DestinationUrl = "https://example.com/a",
            TrackingSlug = "user-a-link"
        });
        Assert.NotNull(userAFunnel);

        // User B creates their own channel
        await analytics.UpsertUserChannelAsync("user-b", isAdmin: false, new ChannelMetrics
        {
            ChannelName = "User B Studio",
            ChannelHandle = "@userbstudio",
            Platform = SocialPlatform.Instagram,
            FollowersOrSubscribers = 3000,
            TotalViewsLast30Days = 45000
        });

        // Anonymous controller call sees ZERO channels, summaries, or funnels
        var anonCtrl = BuildAnalyticsController(analytics, lookup, cfgService, yt, principal: new ClaimsPrincipal(new ClaimsIdentity()));
        var anonChannels = Assert.IsType<OkObjectResult>((await anonCtrl.GetChannels()).Result).Value as List<ChannelMetrics>;
        var anonOverview = Assert.IsType<OkObjectResult>((await anonCtrl.GetOverview()).Result).Value as List<PlatformSummary>;
        Assert.Empty(anonChannels!);
        Assert.Empty(anonOverview!);

        var anonFunnelCtrl = BuildFunnelController(funnels, new ClaimsPrincipal(new ClaimsIdentity()));
        var anonFunnels = Assert.IsType<OkObjectResult>((await anonFunnelCtrl.GetCampaigns()).Result).Value as List<FunnelCampaign>;
        Assert.Empty(anonFunnels!);

        // User A sees ONLY User A's data (not Admin's, not User B's)
        var userAPrincipal = MakePrincipal("user-a", "User");
        var userACtrl = BuildAnalyticsController(analytics, lookup, cfgService, yt, userAPrincipal);
        var userAChannels = Assert.IsType<OkObjectResult>((await userACtrl.GetChannels()).Result).Value as List<ChannelMetrics>;
        Assert.Single(userAChannels!);
        Assert.Equal("@useracreator", userAChannels![0].ChannelHandle);

        // User B cannot delete User A's channel or funnel
        Assert.False(await analytics.DeleteUserChannelAsync("user-b", isAdmin: false, userAChan!.Id));
        Assert.False(await funnels.DeleteUserCampaignAsync("user-b", isAdmin: false, userAFunnel!.Id));

        // Admin sees Admin's channels
        var adminCtrl = BuildAnalyticsController(analytics, lookup, cfgService, yt, MakePrincipal("admin", "Admin"));
        var adminChannels = Assert.IsType<OkObjectResult>((await adminCtrl.GetChannels()).Result).Value as List<ChannelMetrics>;
        Assert.Single(adminChannels!);
        Assert.Equal("@keshavsinghworld", adminChannels![0].ChannelHandle);
    }

    [Fact]
    public async Task SearchAndLookup_AutoFindsChannelsViaApiAndKeylessFallback()
    {
        var published = DateTime.UtcNow.AddDays(-2).ToString("O");
        var handler = new StubHttpHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("youtube.com/results"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        <html><script>var ytInitialData = {"contents":{"channelRenderer":{"channelId":"UC8888888888888888888888","title":{"simpleText":"Creator Demo"},"navigationEndpoint":{"browseEndpoint":{"canonicalBaseUrl":"/@creatordemo"}},"subscriberCountText":{"simpleText":"@creatordemo"},"videoCountText":{"simpleText":"42.5K subscribers"}}}};</script></html>
                        """, Encoding.UTF8, "text/html")
                };
            }
            if (url.Contains("youtube.com/@creatordemo"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""<html><meta property="og:title" content="Creator Demo"><script>{"externalId":"UC8888888888888888888888","subscriberCountText":{"simpleText":"42.5K subscribers"}}</script></html>""")
                };
            }
            if (url.Contains("UC8888888888888888888888"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"""
                        <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
                          <entry><yt:videoId>vidDemo1</yt:videoId><title>AI Coding Guide</title><published>{published}</published><media:group><media:community><media:statistics views="12000"/></media:community></media:group></entry>
                        </feed>
                        """)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var store = CreateStore();
        var cfgService = new SocialConfigService(store);
        var analytics = new AnalyticsEngineService(store, cfgService);
        var yt = new YouTubeLiveFetchService(new StubHttpClientFactory(handler), NullLogger<YouTubeLiveFetchService>.Instance);
        var lookup = new ChannelLookupService(cfgService, yt, new RevenueCalculatorService(cfgService), new GrowthAdvisorService(store, analytics));

        var hits = await lookup.SearchAsync("Creator Demo", SocialPlatform.YouTube);
        var hit = Assert.Single(hits);
        Assert.Equal("@creatordemo", hit.ChannelHandle);
        Assert.Equal(42500, hit.FollowersOrSubscribers);

        var analysis = await lookup.AnalyzeAsync(new PublicChannelLookupRequest { Query = "Creator Demo", Platform = SocialPlatform.YouTube });
        Assert.True(analysis.ResolvedLive);
        Assert.Equal(12000, analysis.Channel.TotalViewsLast30Days);
        Assert.True(analysis.Earnings.TotalEstimatedMonthlyRevenue > 0);
        Assert.NotEmpty(analysis.Audit.PrioritySuggestions);
        Assert.NotEmpty(analysis.AutoInsights);
    }

    [Fact]
    public async Task SiteConfig_SanitizesAdSenseIdsAndRejectsUnsafeInputs()
    {
        var cfgService = new SocialConfigService(CreateStore());
        var saved = await cfgService.SaveSiteConfigAsync(new SiteConfig
        {
            HeroTitle = "Creator Hub",
            Ads = new AdMonetizationConfig
            {
                AdsEnabled = true,
                AdSenseClientId = "ca-pub-1234567890123456",
                HeaderAdSlotId = "9876543210",
                InFeedAdSlotId = "<script>alert(1)</script>",
                SponsorUrl = "javascript:alert(1)"
            }
        });

        Assert.True(saved.Ads.AdsEnabled);
        Assert.Equal("ca-pub-1234567890123456", saved.Ads.AdSenseClientId);
        Assert.Equal("9876543210", saved.Ads.HeaderAdSlotId);
        Assert.Equal(string.Empty, saved.Ads.InFeedAdSlotId);
        Assert.Equal(string.Empty, saved.Ads.SponsorUrl);
    }

    [Fact]
    public async Task YouTubeOAuthService_EnforcesAuthorizeAndBindsStatePerUser()
    {
        var authAttrConfig = typeof(YouTubeOAuthController).GetMethod(nameof(YouTubeOAuthController.GetPublicConfig))?.GetCustomAttribute<AllowAnonymousAttribute>();
        var authAttrStart = typeof(YouTubeOAuthController).GetMethod(nameof(YouTubeOAuthController.StartConnect))?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttrConfig);
        Assert.NotNull(authAttrStart);

        var store = CreateStore();
        var cfgService = new SocialConfigService(store);
        await cfgService.UpdateIntegrationSettingsAsync(new UpdateIntegrationSettingsRequest
        {
            GoogleClientId = "test-client-id",
            GoogleClientSecret = "test-client-secret",
            OAuthRedirectUri = "https://social.keshavsingh.in/youtube/callback"
        });

        var yt = new YouTubeLiveFetchService(new StubHttpClientFactory(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), NullLogger<YouTubeLiveFetchService>.Instance);
        var oauthSvc = new YouTubeOAuthService(
            store,
            cfgService,
            new AnalyticsEngineService(store, cfgService),
            yt,
            new StubHttpClientFactory(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))),
            new EphemeralDataProtectionProvider(),
            NullLogger<YouTubeOAuthService>.Instance);

        var startUrl = await oauthSvc.BeginConnectAsync("user-1", "/dashboard", "https://social.keshavsingh.in");
        Assert.Contains("accounts.google.com/o/oauth2/v2/auth", startUrl);
        Assert.Contains("test-client-id", startUrl);

        var stateStart = startUrl.IndexOf("state=", StringComparison.Ordinal) + 6;
        var stateEnd = startUrl.IndexOf('&', stateStart);
        var state = Uri.UnescapeDataString(startUrl[stateStart..stateEnd]);

        // Different user cannot redeem user-1's state
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            oauthSvc.CompleteConnectAsync("user-2", false, "auth-code", state));
    }

    [Theory]
    [InlineData("""{"header":{"pageHeaderRenderer":{"content":{"pageHeaderViewModel":{"metadata":{"contentMetadataViewModel":{"metadataRows":[{"metadataParts":[{"text":{"content":"1.85K subscribers"}}]}]}}}}}}}""", 1850)]
    [InlineData("""{"accessibility":{"accessibilityData":{"label":"642 subscribers"}}}""", 642)]
    [InlineData("""{"subscriberCountText":{"simpleText":"12.4K subscribers"}}""", 12400)]
    public void ExtractSubscriberCountFromHtml_ParsesModernAndLegacyYouTubeMarkup(string html, long expected)
    {
        Assert.Equal(expected, YouTubeLiveFetchService.ExtractSubscriberCountFromHtml(html));
    }

    [Fact]
    public void GrowthAdvisor_GeneratesRichPillarsIssuesVideoAuditsAndAiPrompts()
    {
        var ch = new ChannelMetrics
        {
            ChannelName = "Omkar Shiv Bhakti",
            ChannelHandle = "@omkarshivbhakti",
            Platform = SocialPlatform.YouTube,
            Niche = "Devotional",
            DataSource = "YouTubeRSS",
            IsVerifiedLive = true,
            FollowersOrSubscribers = 0,
            TotalViewsLast30Days = 3383,
            EngagementRatePercentage = 4.2,
            AverageViewDurationMinutes = 1.2,
            RpmUsd = 2.1,
            EstimatedMonthlyRevenue = 7.1,
            RecentVideos =
            [
                new RecentVideoItem
                {
                    VideoId = "v1",
                    Title = "Bholenath ji ki kripa | Bholenath status #shorts #whatsappstatus #shiv",
                    Url = "https://www.youtube.com/shorts/v1",
                    PublishedAt = DateTime.UtcNow.AddDays(-1),
                    Views = 1320,
                    DescriptionSnippet = "Daily mantra"
                },
                new RecentVideoItem
                {
                    VideoId = "v2",
                    Title = "Har Har Mahadev status #shorts",
                    Url = "https://www.youtube.com/shorts/v2",
                    PublishedAt = DateTime.UtcNow.AddDays(-3),
                    Views = 65,
                    DescriptionSnippet = "Short clip"
                },
                new RecentVideoItem
                {
                    VideoId = "v3",
                    Title = "Om Namah Shivaya Chant #shorts",
                    Url = "https://www.youtube.com/shorts/v3",
                    PublishedAt = DateTime.UtcNow.AddDays(-6),
                    Views = 410,
                    DescriptionSnippet = "Short chant"
                }
            ]
        };

        var audit = GrowthAdvisorService.BuildAuditResult(
            ch,
            ch.ChannelHandle,
            SocialPlatform.YouTube,
            SocialSeedData.DefaultGrowthConfig(),
            SocialSeedData.DefaultSuggestions(),
            new SiteConfig { DefaultDestinationBaseUrl = "https://panchang.keshavsingh.in", Hashtags = "#OmNamahShivaya" });

        Assert.NotNull(audit.Snapshot);
        Assert.Equal(3, audit.Snapshot!.ShortsCount);
        Assert.Equal(0, audit.Snapshot.LongFormCount);
        Assert.Equal(5, audit.ScorePillars.Count);
        Assert.Contains(audit.DetectedIssues, i => i.Id == "issue-zero-subs");
        Assert.Contains(audit.DetectedIssues, i => i.Id == "issue-no-links");
        Assert.Contains(audit.DetectedIssues, i => i.Id == "issue-all-shorts");
        Assert.Contains(audit.DetectedIssues, i => i.Id == "issue-long-titles");
        Assert.Equal(3, audit.VideoAudits.Count);
        Assert.Contains(audit.VideoAudits, v => v.PerformanceTag == "Breakout" && v.VideoId == "v1");
        Assert.Contains(audit.VideoAudits, v => v.PerformanceTag == "Underperforming" && v.VideoId == "v2");
        Assert.True(audit.PromptTemplates.Count >= 4);
        Assert.All(audit.PromptTemplates, pt => Assert.Contains("@omkarshivbhakti", pt.PromptText));
        Assert.Contains("https://panchang.keshavsingh.in", audit.RecommendedDescriptionTemplate);
    }

    private static ClaimsPrincipal MakePrincipal(string userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static AnalyticsController BuildAnalyticsController(
        AnalyticsEngineService analytics,
        ChannelLookupService lookup,
        SocialConfigService cfg,
        YouTubeLiveFetchService yt,
        ClaimsPrincipal principal) =>
        new(analytics, lookup, cfg, yt)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

    private static TrafficFunnelController BuildFunnelController(FunnelService funnels, ClaimsPrincipal principal) =>
        new(funnels)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

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
