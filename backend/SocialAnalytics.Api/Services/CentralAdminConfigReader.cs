using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace SocialAnalytics.Api.Services;

public sealed record CentralAdminIntegrationsSnapshot(
    string? YouTubeApiKey,
    string? GoogleOAuthClientId,
    string? GoogleOAuthClientSecret,
    string? AdSenseClientId);

/// <summary>
/// Reads shared third-party credentials directly from Admin's central MongoDB (<c>AdminDb.settings</c>)
/// and decrypts AES-256-GCM ciphertexts produced by <c>KeshavSingh.Security.DataProtector</c>.
/// Secrets stay strictly server-to-DB — never transmitted over HTTP between ecosystem services.
/// </summary>
public sealed class CentralAdminConfigReader
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte FormatVersion = 1;
    private const int HeaderSize = 2;

    private readonly IMongoCollection<CentralAppSettingsDocument>? _collection;
    private readonly string _documentId;
    private readonly byte _currentKeyId;
    private readonly IReadOnlyDictionary<byte, byte[]> _keys;
    private readonly ILogger<CentralAdminConfigReader> _logger;
    private readonly Func<CancellationToken, Task<CentralAdminIntegrationsSnapshot?>>? _overrideLoader;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CentralAdminIntegrationsSnapshot? _cached;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;
    private DateTime _lastKeyWarningUtc = DateTime.MinValue;

    public CentralAdminConfigReader(IConfiguration configuration, ILogger<CentralAdminConfigReader> logger)
    {
        _logger = logger;
        _documentId = FirstNonEmpty(configuration["CentralMongo:DocumentId"]) ?? "app-settings";
        var enabled = !string.Equals(configuration["CentralMongo:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        _keys = LoadEncryptionKeys(configuration, out _currentKeyId, _logger);

        if (!enabled) return;

        var connString = FirstNonEmpty(
            configuration["CentralMongo:ConnectionString"],
            configuration["Mongo:ConnectionString"],
            configuration["MongoDbSettings:ConnectionString"]);
        var dbName = FirstNonEmpty(configuration["CentralMongo:DatabaseName"]) ?? "AdminDb";
        var colName = FirstNonEmpty(configuration["CentralMongo:CollectionName"]) ?? "settings";

        if (string.IsNullOrWhiteSpace(connString)) return;

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            var client = new MongoClient(settings);
            _collection = client.GetDatabase(dbName).GetCollection<CentralAppSettingsDocument>(colName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Central Admin Mongo client could not be initialized; using local/env fallbacks.");
        }
    }

    internal CentralAdminConfigReader(
        Func<CancellationToken, Task<CentralAdminIntegrationsSnapshot?>> overrideLoader,
        ILogger<CentralAdminConfigReader> logger)
    {
        _overrideLoader = overrideLoader;
        _logger = logger;
        _documentId = "app-settings";
        _keys = new Dictionary<byte, byte[]>();
        _currentKeyId = 1;
    }

    public bool IsConfigured => _collection is not null || _overrideLoader is not null;

    public async Task<CentralAdminIntegrationsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return EmptySnapshot;

        var now = DateTime.UtcNow;
        if (_cached is not null && now < _cacheExpiresAtUtc)
            return _cached;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = DateTime.UtcNow;
            if (_cached is not null && now < _cacheExpiresAtUtc)
                return _cached;

            CentralAdminIntegrationsSnapshot? loaded;
            if (_overrideLoader is not null)
            {
                loaded = await _overrideLoader(ct).ConfigureAwait(false) ?? EmptySnapshot;
            }
            else
            {
                var doc = await _collection!
                    .Find(Builders<CentralAppSettingsDocument>.Filter.Eq(x => x.Id, _documentId))
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                loaded = doc is null ? EmptySnapshot : DecryptDocument(doc);
            }

            _cached = loaded;
            _cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(60);
            return loaded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Unable to read shared credentials from Central Admin Mongo; using local/env fallbacks.");
            _cached = EmptySnapshot;
            _cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(20);
            return EmptySnapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void InvalidateCache() => _cacheExpiresAtUtc = DateTime.MinValue;

    private CentralAdminIntegrationsSnapshot DecryptDocument(CentralAppSettingsDocument doc)
    {
        var ytKey = TryDecryptField(doc.YouTubeApiKeyEncrypted, "YouTubeApiKeyEncrypted");
        var googleSecret = TryDecryptField(doc.GoogleOAuthClientSecretEncrypted, "GoogleOAuthClientSecretEncrypted");
        var googleClientId = FirstNonEmpty(doc.GoogleOAuthClientId);
        var adSenseId = FirstNonEmpty(doc.AdSenseClientId);

        return new CentralAdminIntegrationsSnapshot(ytKey, googleClientId, googleSecret, adSenseId);
    }

    private string? TryDecryptField(string? cipherText, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) return null;
        if (_keys.Count == 0)
        {
            if ((DateTime.UtcNow - _lastKeyWarningUtc).TotalMinutes >= 1)
            {
                _lastKeyWarningUtc = DateTime.UtcNow;
                _logger.LogWarning("Central Admin document has {Field}, but Encryption:DataKey is not configured.", fieldName);
            }
            return null;
        }

        try
        {
            return FirstNonEmpty(DecryptProtectedValue(cipherText, _keys, _currentKeyId));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning("Failed to decrypt Central Admin {Field}; check Encryption:DataKey matches Admin.", fieldName);
            return null;
        }
    }

    internal static string DecryptProtectedValue(
        string protectedValue,
        IReadOnlyDictionary<byte, byte[]> keys,
        byte currentKeyId)
    {
        var data = Convert.FromBase64String(protectedValue);
        var versioned = data.Length >= HeaderSize + NonceSize + TagSize && data[0] == FormatVersion;
        var offset = versioned ? HeaderSize : 0;

        if (data.Length < offset + NonceSize + TagSize)
            throw new CryptographicException("Protected secret ciphertext is malformed.");

        var keyId = versioned ? data[1] : currentKeyId;
        if (!keys.TryGetValue(keyId, out var key))
            throw new CryptographicException($"Encryption key id {keyId} is not configured.");

        var nonce = data.AsSpan(offset, NonceSize);
        var tag = data.AsSpan(offset + NonceSize, TagSize);
        var cipher = data.AsSpan(offset + NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    internal static IReadOnlyDictionary<byte, byte[]> LoadEncryptionKeys(
        IConfiguration configuration,
        out byte currentKeyId,
        ILogger? logger = null)
    {
        currentKeyId = byte.TryParse(configuration["Encryption:DataKeyId"], out var parsedId) ? parsedId : (byte)1;
        var keys = new Dictionary<byte, byte[]>();

        var primaryRaw = FirstNonEmpty(configuration["Encryption:DataKey"]);
        if (!string.IsNullOrEmpty(primaryRaw) && TryParseAesKey(primaryRaw, out var primaryBytes))
        {
            keys[currentKeyId] = primaryBytes;
        }
        else if (!string.IsNullOrEmpty(primaryRaw))
        {
            logger?.LogWarning("Encryption:DataKey must be a Base64-encoded 32-byte key.");
        }

        foreach (var child in configuration.GetSection("Encryption:PreviousDataKeys").GetChildren())
        {
            if (byte.TryParse(child["KeyId"], out var retiredId) &&
                TryParseAesKey(child["Key"], out var retiredBytes) &&
                !keys.ContainsKey(retiredId))
            {
                keys[retiredId] = retiredBytes;
            }
        }

        return keys;
    }

    private static bool TryParseAesKey(string? raw, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            var decoded = Convert.FromBase64String(raw.Trim());
            if (decoded.Length != 32) return false;
            key = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim();

    private static readonly CentralAdminIntegrationsSnapshot EmptySnapshot = new(null, null, null, null);

    [BsonIgnoreExtraElements]
    private sealed class CentralAppSettingsDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;
        public string? YouTubeApiKeyEncrypted { get; set; }
        public string? GoogleOAuthClientId { get; set; }
        public string? GoogleOAuthClientSecretEncrypted { get; set; }
        public string? AdSenseClientId { get; set; }
    }
}
