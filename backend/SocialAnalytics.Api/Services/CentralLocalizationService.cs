using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public sealed class SocialLocalizationConfig
{
    public string Id { get; set; } = "default";
    public bool OverrideCentralLocalization { get; set; } = false;
    public string DefaultLocale { get; set; } = "en";
    public List<string> EnabledLocales { get; set; } = ["en", "hi"];
    public bool AllowVisitorLanguageSwitch { get; set; } = true;
    public bool ShowLanguagePicker { get; set; } = true;
    public bool ForceLocale { get; set; } = false;
    public Dictionary<string, Dictionary<string, string>> LocalOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastUpdatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record SocialLocaleDto(
    string Code,
    string NativeName,
    string EnglishName,
    string Direction = "ltr",
    string Icon = "",
    bool IsDefault = false);

public sealed record EffectiveLocalizationPolicyDto(
    string SiteKey,
    string DefaultLocale,
    IReadOnlyList<string> EnabledLocales,
    bool AllowVisitorLanguageSwitch,
    bool ShowLanguagePicker,
    bool ForceLocale,
    string ControlledBy,
    bool OverrideCentralLocalization);

public sealed record LocalizationManifestResponse(
    EffectiveLocalizationPolicyDto Policy,
    IReadOnlyList<SocialLocaleDto> Locales,
    IReadOnlyDictionary<string, string> Versions,
    string Source);

public sealed record LocalizationBundleResponse(
    string Locale,
    string Version,
    EffectiveLocalizationPolicyDto Policy,
    IReadOnlyDictionary<string, string> Entries,
    string Source);

public sealed record SiteLocalizationAdminStateResponse(
    EffectiveLocalizationPolicyDto CentralAdminPolicy,
    SocialLocalizationConfig LocalSettings,
    EffectiveLocalizationPolicyDto EffectivePolicy);

public sealed record UpdateSiteLocalizationRequest(
    bool OverrideCentralLocalization,
    string DefaultLocale,
    IReadOnlyList<string>? EnabledLocales,
    bool AllowVisitorLanguageSwitch,
    bool ShowLanguagePicker,
    bool ForceLocale,
    Dictionary<string, Dictionary<string, string>>? LocalOverrides = null);

/// <summary>
/// Direct AdminDb MongoDB localization + ecosystem config reader for SocialPulse Analytics,
/// supporting central Admin control plus optional per-site override without HTTP config loops.
/// </summary>
public sealed class CentralLocalizationService
{
    private const string SiteKey = "social";
    private const string SettingsCollection = "localization_settings";
    private static readonly string[] Namespaces = ["ecosystem", "common", "social"];
    private static readonly ConcurrentDictionary<string, MongoClient> Clients = new(StringComparer.Ordinal);

    private readonly HybridDataStore _store;
    private readonly string? _centralConnectionString;
    private readonly string _centralDbName;
    private readonly ILogger<CentralLocalizationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CentralSnapshot? _cached;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

    public CentralLocalizationService(
        HybridDataStore store,
        IConfiguration configuration,
        ILogger<CentralLocalizationService> logger)
    {
        _store = store;
        _centralConnectionString = FirstNonEmpty(
            configuration["CentralMongo:ConnectionString"],
            Environment.GetEnvironmentVariable("CentralMongo__ConnectionString"),
            configuration["Mongo:ConnectionString"],
            Environment.GetEnvironmentVariable("Mongo__ConnectionString"),
            configuration["MongoDbSettings:ConnectionString"],
            Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING"));
        _centralDbName = FirstNonEmpty(
            configuration["CentralMongo:DatabaseName"],
            Environment.GetEnvironmentVariable("CentralMongo__DatabaseName")) ?? "AdminDb";
        _logger = logger;
    }

    public async Task<LocalizationManifestResponse> GetManifestAsync(CancellationToken ct = default)
    {
        var snap = await GetSnapshotAsync(ct);
        var local = await GetLocalConfigAsync();
        var policy = ResolvePolicy(snap.CentralPolicy, local);
        var locales = FilterLocales(snap.Locales, policy);
        var versions = locales.ToDictionary(
            l => l.Code,
            l => $"{l.Code}:{snap.LoadedAtUtc.Ticks:x}:{local.UpdatedAtUtc.Ticks:x}",
            StringComparer.OrdinalIgnoreCase);
        return new LocalizationManifestResponse(
            policy,
            locales,
            versions,
            snap.LoadedFromCentralMongo ? "mongodb-central" : "builtin-fallback");
    }

    public async Task<LocalizationBundleResponse> GetBundleAsync(string? requestedLocale, CancellationToken ct = default)
    {
        var snap = await GetSnapshotAsync(ct);
        var local = await GetLocalConfigAsync();
        var policy = ResolvePolicy(snap.CentralPolicy, local);

        var requested = NormalizeLocale(requestedLocale, policy.DefaultLocale);
        var effectiveLocale = policy.ForceLocale
            ? policy.DefaultLocale
            : (policy.EnabledLocales.Contains(requested, StringComparer.OrdinalIgnoreCase)
                ? requested
                : policy.DefaultLocale);

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ApplyEntries(merged, BuiltInFallback(effectiveLocale));
        if (snap.TranslationsByLocale.TryGetValue("en", out var centralEn))
            ApplyEntries(merged, centralEn);
        if (snap.TranslationsByLocale.TryGetValue(effectiveLocale, out var centralTarget))
            ApplyEntries(merged, centralTarget);
        if (local.OverrideCentralLocalization && local.LocalOverrides.TryGetValue(effectiveLocale, out var localMap))
            ApplyEntries(merged, localMap);

        var version = $"{effectiveLocale}:{snap.LoadedAtUtc.Ticks:x}:{local.UpdatedAtUtc.Ticks:x}";
        return new LocalizationBundleResponse(
            effectiveLocale,
            version,
            policy,
            merged,
            snap.LoadedFromCentralMongo ? "mongodb-central" : "builtin-fallback");
    }

    public async Task<SiteLocalizationAdminStateResponse> GetAdminStateAsync(CancellationToken ct = default)
    {
        var snap = await GetSnapshotAsync(ct);
        var local = await GetLocalConfigAsync();
        return new SiteLocalizationAdminStateResponse(
            snap.CentralPolicy,
            local,
            ResolvePolicy(snap.CentralPolicy, local));
    }

    public async Task<SiteLocalizationAdminStateResponse> UpdateLocalSettingsAsync(
        UpdateSiteLocalizationRequest req,
        string adminUserId,
        CancellationToken ct = default)
    {
        var existing = await GetLocalConfigAsync();
        var defaultLocale = NormalizeLocale(req.DefaultLocale, "en");
        var enabled = NormalizeEnabledLocales(req.EnabledLocales, defaultLocale);

        var updated = new SocialLocalizationConfig
        {
            Id = "default",
            OverrideCentralLocalization = req.OverrideCentralLocalization,
            DefaultLocale = defaultLocale,
            EnabledLocales = enabled.ToList(),
            AllowVisitorLanguageSwitch = req.AllowVisitorLanguageSwitch,
            ShowLanguagePicker = req.ShowLanguagePicker,
            ForceLocale = req.ForceLocale,
            LocalOverrides = req.LocalOverrides ?? existing.LocalOverrides,
            LastUpdatedByUserId = adminUserId,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await _store.SaveSingletonAsync(SettingsCollection, "default", updated);
        InvalidateCache();
        return await GetAdminStateAsync(ct);
    }

    public async Task<CentralConfigEnvelope?> TryLoadCentralEnvelopeFromMongoAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_centralConnectionString))
            return null;

        try
        {
            var client = Clients.GetOrAdd(_centralConnectionString, cs => new MongoClient(cs));
            var db = client.GetDatabase(_centralDbName);

            var siteDocs = await db.GetCollection<BsonDocument>("websites")
                .Find(Builders<BsonDocument>.Filter.Eq("IsEnabled", true))
                .ToListAsync(ct);

            var channelDocs = await db.GetCollection<BsonDocument>("creator_channels")
                .Find(Builders<BsonDocument>.Filter.Eq("IsEnabled", true))
                .ToListAsync(ct);

            var cfgDocs = await db.GetCollection<BsonDocument>("config_entries")
                .Find(Builders<BsonDocument>.Filter.Ne("IsSecret", true))
                .ToListAsync(ct);

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in cfgDocs)
            {
                var k = d.GetValue("Key", "").AsString;
                var val = d.TryGetValue("Value", out var v) && v.IsString && !string.IsNullOrWhiteSpace(v.AsString)
                    ? v.AsString
                    : d.GetValue("DefaultValue", "").AsString;
                if (!string.IsNullOrWhiteSpace(k))
                    values[k] = val;
            }

            return new CentralConfigEnvelope
            {
                SiteTitle = "Keshav Ecosystem",
                Values = values,
                Websites = siteDocs.Select(d => new CentralWebsiteDto
                {
                    Key = d.GetValue("Key", "").AsString,
                    Name = d.GetValue("Name", "").AsString,
                    Url = d.GetValue("Url", "").AsString,
                    Description = d.GetValue("Description", "").AsString,
                    Icon = d.GetValue("Icon", "🌐").AsString,
                    SortOrder = d.TryGetValue("SortOrder", out var so) && so.IsInt32 ? so.AsInt32 : 10,
                    ShowInEcosystemBar = !d.TryGetValue("ShowInEcosystemBar", out var sb) || !sb.IsBoolean || sb.AsBoolean,
                }).ToList(),
                Channels = channelDocs.Select(d => new CentralChannelDto
                {
                    Id = d.TryGetValue("_id", out var id) ? id.ToString() ?? string.Empty : string.Empty,
                    Key = d.GetValue("Key", "").AsString,
                    Name = d.GetValue("Name", "").AsString,
                    Handle = d.GetValue("Handle", "").AsString,
                    Platform = d.GetValue("Platform", "YouTube").AsString,
                    ChannelId = d.GetValue("ChannelId", "").AsString,
                    Url = d.GetValue("Url", "").AsString,
                    Niche = d.GetValue("Niche", "").AsString,
                    Tagline = d.GetValue("Tagline", "").AsString,
                    Description = d.GetValue("Description", "").AsString,
                    ShowInAnalytics = d.TryGetValue("ShowInAnalytics", out var sa) && sa.IsBoolean && sa.AsBoolean,
                    IsEnabled = !d.TryGetValue("IsEnabled", out var ie) || !ie.IsBoolean || ie.AsBoolean,
                }).ToList(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Direct AdminDb envelope read failed; HTTP fallback will be used.");
            return null;
        }
    }

    public static EffectiveLocalizationPolicyDto ResolvePolicy(
        EffectiveLocalizationPolicyDto central,
        SocialLocalizationConfig local)
    {
        if (!local.OverrideCentralLocalization)
            return central with { ControlledBy = "admin", OverrideCentralLocalization = false };

        var def = NormalizeLocale(local.DefaultLocale, central.DefaultLocale);
        var enabled = NormalizeEnabledLocales(local.EnabledLocales, def);
        return new EffectiveLocalizationPolicyDto(
            SiteKey,
            def,
            enabled,
            local.AllowVisitorLanguageSwitch,
            local.ShowLanguagePicker,
            local.ForceLocale,
            "site-override",
            true);
    }

    public void InvalidateCache() => _cacheExpiresAtUtc = DateTime.MinValue;

    private Task<SocialLocalizationConfig> GetLocalConfigAsync() =>
        _store.GetSingletonAsync(SettingsCollection, "default", () => new SocialLocalizationConfig());

    private async Task<CentralSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_cached is not null && now < _cacheExpiresAtUtc)
            return _cached;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = DateTime.UtcNow;
            if (_cached is not null && now < _cacheExpiresAtUtc)
                return _cached;

            var snapshot = await LoadCentralSnapshotAsync(ct).ConfigureAwait(false);
            _cached = snapshot;
            _cacheExpiresAtUtc = now.AddSeconds(60);
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CentralSnapshot> LoadCentralSnapshotAsync(CancellationToken ct)
    {
        var defaultPolicy = new EffectiveLocalizationPolicyDto(
            SiteKey, "en", ["en", "hi"], true, true, false, "admin", false);

        if (string.IsNullOrWhiteSpace(_centralConnectionString))
        {
            return new CentralSnapshot(defaultPolicy, DefaultLocales(), new(StringComparer.OrdinalIgnoreCase), false, DateTime.UtcNow);
        }

        try
        {
            var client = Clients.GetOrAdd(_centralConnectionString, cs => new MongoClient(cs));
            var db = client.GetDatabase(_centralDbName);

            var websiteFilter = Builders<BsonDocument>.Filter.In("Key", [SiteKey, "socialanalytics"]);
            var websiteDoc = await db.GetCollection<BsonDocument>("websites").Find(websiteFilter).FirstOrDefaultAsync(ct);

            var centralPolicy = defaultPolicy;
            if (websiteDoc is not null)
            {
                var def = NormalizeLocale(websiteDoc.GetValue("DefaultLocale", "en").AsString, "en");
                var enabled = ReadStringArray(websiteDoc, "EnabledLocales");
                centralPolicy = new EffectiveLocalizationPolicyDto(
                    SiteKey,
                    def,
                    NormalizeEnabledLocales(enabled, def),
                    !websiteDoc.TryGetValue("AllowVisitorLanguageSwitch", out var av) || !av.IsBoolean || av.AsBoolean,
                    !websiteDoc.TryGetValue("ShowLanguagePicker", out var sp) || !sp.IsBoolean || sp.AsBoolean,
                    websiteDoc.TryGetValue("ForceLocale", out var fl) && fl.IsBoolean && fl.AsBoolean,
                    "admin",
                    false);
            }

            var localeDocs = await db.GetCollection<BsonDocument>("locales")
                .Find(Builders<BsonDocument>.Filter.Eq("IsEnabled", true))
                .ToListAsync(ct);

            var locales = localeDocs.Count > 0
                ? localeDocs.Select(d => new SocialLocaleDto(
                    NormalizeLocale(d.GetValue("Code", "en").AsString, "en"),
                    d.GetValue("NativeName", "English").AsString,
                    d.GetValue("EnglishName", "English").AsString,
                    d.GetValue("Direction", "ltr").AsString,
                    d.GetValue("Icon", "").AsString,
                    false)).ToList()
                : DefaultLocales();

            var trDocs = await db.GetCollection<BsonDocument>("translations")
                .Find(Builders<BsonDocument>.Filter.In("Namespace", Namespaces))
                .ToListAsync(ct);

            var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in trDocs)
            {
                var key = doc.GetValue("Key", "").AsString;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!doc.TryGetValue("Values", out var vals) || !vals.IsBsonDocument) continue;
                foreach (var el in vals.AsBsonDocument.Elements)
                {
                    var loc = NormalizeLocale(el.Name, string.Empty);
                    var text = el.Value.IsString ? el.Value.AsString : string.Empty;
                    if (string.IsNullOrWhiteSpace(loc) || string.IsNullOrWhiteSpace(text)) continue;
                    if (!translations.TryGetValue(loc, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        translations[loc] = map;
                    }
                    map[key] = text;
                }
            }

            return new CentralSnapshot(centralPolicy, locales, translations, true, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Central AdminDb localization lookup failed; using built-in bilingual fallback.");
            return new CentralSnapshot(defaultPolicy, DefaultLocales(), new(StringComparer.OrdinalIgnoreCase), false, DateTime.UtcNow);
        }
    }

    private static List<string> ReadStringArray(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out var val) || !val.IsBsonArray) return [];
        return val.AsBsonArray.Where(x => x.IsString).Select(x => x.AsString).ToList();
    }

    private static List<SocialLocaleDto> FilterLocales(
        IReadOnlyList<SocialLocaleDto> all,
        EffectiveLocalizationPolicyDto policy)
    {
        var enabled = policy.EnabledLocales.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = all.Where(l => enabled.Contains(l.Code)).ToList();
        if (list.Count == 0)
            list = DefaultLocales().Where(l => enabled.Contains(l.Code)).ToList();
        return list.Select(l => l with { IsDefault = string.Equals(l.Code, policy.DefaultLocale, StringComparison.OrdinalIgnoreCase) }).ToList();
    }

    private static void ApplyEntries(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var kv in source)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                target[kv.Key] = kv.Value;
        }
    }

    public static string NormalizeLocale(string? locale, string fallback = "en")
    {
        var raw = (locale ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.StartsWith("hi", StringComparison.Ordinal)) return "hi";
        if (raw.StartsWith("en", StringComparison.Ordinal)) return "en";
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
    }

    public static IReadOnlyList<string> NormalizeEnabledLocales(IEnumerable<string>? locales, string defaultLocale)
    {
        var list = new List<string>();
        if (locales is not null)
        {
            foreach (var raw in locales)
            {
                var norm = NormalizeLocale(raw, string.Empty);
                if (!string.IsNullOrWhiteSpace(norm) && !list.Contains(norm, StringComparer.OrdinalIgnoreCase))
                    list.Add(norm);
            }
        }
        if (list.Count == 0)
        {
            list.Add("en");
            list.Add("hi");
        }
        var def = NormalizeLocale(defaultLocale, "en");
        if (!list.Contains(def, StringComparer.OrdinalIgnoreCase))
            list.Insert(0, def);
        return list;
    }

    private static List<SocialLocaleDto> DefaultLocales() =>
    [
        new("en", "English", "English", "ltr", "🌐", true),
        new("hi", "हिन्दी", "Hindi", "ltr", "🇮🇳", false),
    ];

    private static Dictionary<string, string> BuiltInFallback(string locale) =>
        string.Equals(locale, "hi", StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["social.nav.dashboard"] = "डैशबोर्ड",
                ["social.nav.outliers"] = "आउटलायर व ग्रोथ बेंचमार्क",
                ["social.nav.earnings"] = "कमाई व RPM",
                ["social.nav.advisor"] = "ग्रोथ एडवाइज़र",
                ["social.nav.funnel"] = "ट्रैफिक फ़नल",
                ["social.nav.admin"] = "एडमिन",
                ["social.brand.sub"] = "क्रिएटर एनालिटिक्स एवं ग्रोथ हब",
                ["social.hero.title"] = "सोशल चैनल प्रदर्शन और ग्रोथ हब",
                ["social.hero.subtitle"] = "YouTube, Instagram और Facebook पर लाइव एंगेजमेंट ट्रैक करें, कमाई का अनुमान लगाएं और दर्शक ट्रैफिक बढ़ाएं।",
                ["social.earnings.title"] = "💰 क्रिएटर कमाई एवं RPM कैलकुलेटर",
                ["social.advisor.title"] = "🚀 ग्रोथ एडवाइज़र एवं चैनल फ़िक्स स्टूडियो",
                ["social.funnel.title"] = "🎯 ट्रैफिक फ़नल और लिंक कन्वर्ज़न इंजन",
                ["social.kpi.viewsPerSub"] = "व्यूज़ / सब्सक्राइबर (आउटलायर स्कोर)",
                ["social.seedWarning.title"] = "🚫 कभी भी Paid Subscribers या Sub4Sub न लें — अपनी Seed Audience बचाएं",
                ["social.seedWarning.body"] = "हर नए वीडियो को YouTube सबसे पहले आपके सब्सक्राइबर्स और दर्शकों की Watch History वाली Seed Audience को दिखाकर अन्य वीडियो से तुलना (Compare) करता है। यदि Paid या नकली सब्सक्राइबर्स वीडियो को Skip कर देते हैं, तो शुरुआती CTR व Watch Time गिर जाता है और YouTube आपके वीडियो को नए दर्शकों तक आगे नहीं बढ़ाता (साथ ही YPP Fake Engagement पॉलिसी का जोखिम भी रहता है)। हमेशा केवल वास्तविक (Organic) दर्शक ही जोड़ें।",
                ["ecosystem.language.label"] = "भाषा",
            }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["social.nav.dashboard"] = "Dashboard",
                ["social.nav.outliers"] = "Outliers & Growth",
                ["social.nav.earnings"] = "Earnings & RPM",
                ["social.nav.advisor"] = "Growth Advisor",
                ["social.nav.funnel"] = "Traffic Funnel",
                ["social.nav.admin"] = "Admin",
                ["social.brand.sub"] = "Creator & Social Intelligence",
                ["social.hero.title"] = "Social Channel Performance & Growth Hub",
                ["social.hero.subtitle"] = "Search any YouTube or social handle for instant analytics, RPM forecasts, and growth insights — or sign in to connect your own private channels.",
                ["social.earnings.title"] = "💰 Creator Earnings & RPM Calculator",
                ["social.advisor.title"] = "🚀 Growth Advisor & Channel Fix Studio",
                ["social.funnel.title"] = "🎯 Traffic Funnel & Link Conversion Engine",
                ["social.kpi.viewsPerSub"] = "Views / Subscriber (Outlier Score)",
                ["social.seedWarning.title"] = "🚫 Never Buy Paid Subscribers or Sub4Sub — Protect Your Seed Audience",
                ["social.seedWarning.body"] = "YouTube tests every new upload with a small seed audience matched by subscriber & viewer watch history. If paid or inactive subscribers ignore your video or skip early, your retention and CTR lose when compared against competing videos — stopping organic recommendations and risking YPP Fake Engagement violations. Grow strictly with real viewers interested in your niche.",
                ["ecosystem.language.label"] = "Language",
            };

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private sealed record CentralSnapshot(
        EffectiveLocalizationPolicyDto CentralPolicy,
        IReadOnlyList<SocialLocaleDto> Locales,
        Dictionary<string, Dictionary<string, string>> TranslationsByLocale,
        bool LoadedFromCentralMongo,
        DateTime LoadedAtUtc);
}
