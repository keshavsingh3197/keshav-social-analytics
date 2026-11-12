using System.Collections.Concurrent;
using System.Text.Json;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// Thread-safe hybrid persistence store supporting MongoDB (when Mongo:ConnectionString
/// or MongoDbSettings:ConnectionString is configured) with automatic atomic JSON file
/// storage in App_Data/ as a durable fallback.
/// </summary>
public sealed class HybridDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static int _conventionsRegistered;

    private readonly IMongoDatabase? _mongoDb;
    private readonly string _dataDir;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public bool IsMongoEnabled => _mongoDb is not null;
    public string StorageMode => IsMongoEnabled ? "MongoDB" : "JsonFileStore";

    public HybridDataStore(IConfiguration configuration, IHostEnvironment env, ILogger<HybridDataStore> logger)
    {
        _dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_dataDir);

        var connStr = configuration["Mongo:ConnectionString"]
            ?? configuration["MongoDbSettings:ConnectionString"]
            ?? configuration.GetConnectionString("Mongo");

        var dbName = configuration["Mongo:Database"]
            ?? configuration["MongoDbSettings:Database"]
            ?? "SocialAnalyticsDb";

        if (!string.IsNullOrWhiteSpace(connStr))
        {
            if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 0)
            {
                var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
                ConventionRegistry.Register("SocialAnalyticsIgnoreExtra", pack, _ => true);
            }

            try
            {
                var client = new MongoClient(connStr);
                _mongoDb = client.GetDatabase(dbName);
                logger.LogInformation("SocialAnalytics HybridDataStore initialized with MongoDB database '{Database}'.", dbName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize MongoDB client; falling back to App_Data JSON storage.");
                _mongoDb = null;
            }
        }
        else
        {
            logger.LogInformation("SocialAnalytics HybridDataStore initialized in App_Data JSON file mode at '{Dir}'.", _dataDir);
        }
    }

    private SemaphoreSlim GetLock(string name) => _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));

    private string JsonPath(string collectionName) => Path.Combine(_dataDir, $"{collectionName}.json");

    private string SeedMarkerPath(string collectionName) => Path.Combine(_dataDir, $".{collectionName}.seeded");

    public async Task<List<T>> GetListAsync<T>(string collectionName, Func<List<T>> seedFactory)
    {
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                var existing = await col.Find(FilterDefinition<T>.Empty).ToListAsync();
                if (existing.Count > 0) return existing;

                var metaCol = _mongoDb.GetCollection<SeedMarker>("_seed_markers");
                var seeded = await metaCol.Find(x => x.Id == collectionName).AnyAsync();
                if (!seeded)
                {
                    var seed = seedFactory();
                    if (seed.Count > 0) await col.InsertManyAsync(seed);
                    await metaCol.ReplaceOneAsync(
                        x => x.Id == collectionName,
                        new SeedMarker { Id = collectionName, SeededAt = DateTime.UtcNow },
                        new ReplaceOptions { IsUpsert = true });
                    return seed;
                }
                return existing;
            }

            var path = JsonPath(collectionName);
            if (!File.Exists(path))
            {
                var seed = seedFactory();
                await WriteJsonFileAsync(path, seed);
                await File.WriteAllTextAsync(SeedMarkerPath(collectionName), DateTime.UtcNow.ToString("O"));
                return seed;
            }

            var raw = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return JsonSerializer.Deserialize<List<T>>(raw, JsonOptions) ?? [];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveListAsync<T>(string collectionName, List<T> items)
    {
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                await col.DeleteManyAsync(FilterDefinition<T>.Empty);
                if (items.Count > 0) await col.InsertManyAsync(items);

                var metaCol = _mongoDb.GetCollection<SeedMarker>("_seed_markers");
                await metaCol.ReplaceOneAsync(
                    x => x.Id == collectionName,
                    new SeedMarker { Id = collectionName, SeededAt = DateTime.UtcNow },
                    new ReplaceOptions { IsUpsert = true });
                return;
            }

            await WriteJsonFileAsync(JsonPath(collectionName), items);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> UpsertItemAsync<T>(
        string collectionName,
        string id,
        Func<T, string> idSelector,
        T item,
        Func<List<T>> seedFactory)
    {
        var list = await GetListAsync(collectionName, seedFactory);
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                var filter = Builders<T>.Filter.Eq("_id", id);
                await col.ReplaceOneAsync(filter, item, new ReplaceOptions { IsUpsert = true });
                return item;
            }

            var idx = list.FindIndex(x => string.Equals(idSelector(x), id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) list[idx] = item;
            else list.Add(item);
            await WriteJsonFileAsync(JsonPath(collectionName), list);
            return item;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteItemAsync<T>(
        string collectionName,
        string id,
        Func<T, string> idSelector,
        Func<List<T>> seedFactory)
    {
        var list = await GetListAsync(collectionName, seedFactory);
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                var result = await col.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id));
                return result.DeletedCount > 0;
            }

            var removed = list.RemoveAll(x => string.Equals(idSelector(x), id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) await WriteJsonFileAsync(JsonPath(collectionName), list);
            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> GetSingletonAsync<T>(string collectionName, string id, Func<T> seedFactory) where T : class
    {
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                var doc = await col.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
                if (doc is not null) return doc;

                var seed = seedFactory();
                await col.ReplaceOneAsync(
                    Builders<T>.Filter.Eq("_id", id),
                    seed,
                    new ReplaceOptions { IsUpsert = true });
                return seed;
            }

            var path = JsonPath(collectionName);
            if (!File.Exists(path))
            {
                var seed = seedFactory();
                await WriteJsonFileAsync(path, seed);
                return seed;
            }

            var raw = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(raw, JsonOptions) ?? seedFactory();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> SaveSingletonAsync<T>(string collectionName, string id, T item) where T : class
    {
        var gate = GetLock(collectionName);
        await gate.WaitAsync();
        try
        {
            if (_mongoDb is not null)
            {
                var col = _mongoDb.GetCollection<T>(collectionName);
                await col.ReplaceOneAsync(
                    Builders<T>.Filter.Eq("_id", id),
                    item,
                    new ReplaceOptions { IsUpsert = true });
                return item;
            }

            await WriteJsonFileAsync(JsonPath(collectionName), item);
            return item;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task WriteJsonFileAsync<T>(string path, T payload)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class SeedMarker
    {
        public string Id { get; set; } = string.Empty;
        public DateTime SeededAt { get; set; }
    }
}
