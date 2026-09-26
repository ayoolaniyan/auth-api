using System.Text.Json;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace IdentityServer.Caching;

/// <summary>
/// <see cref="ICache{T}"/> backed by <see cref="IDistributedCache"/> (Redis), so the configuration
/// store cache is shared by every IdentityServer instance instead of kept per process.
/// </summary>
/// <remarks>
/// If Redis is unavailable the error is logged and the value is loaded from the store, so token
/// requests keep working (without caching) until Redis is back.
/// </remarks>
public class DistributedCache<T> : ICache<T> where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCache<T>> _logger;

    public DistributedCache(IDistributedCache cache, ILogger<DistributedCache<T>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync(string key)
    {
        try
        {
            var cached = await _cache.GetAsync(GetKey(key));
            return cached is null ? null : JsonSerializer.Deserialize<T>(cached, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read {CacheKey} from Redis", GetKey(key));
            return null;
        }
    }

    public async Task SetAsync(string key, T item, TimeSpan expiration)
    {
        try
        {
            await _cache.SetAsync(GetKey(key), JsonSerializer.SerializeToUtf8Bytes(item, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write {CacheKey} to Redis", GetKey(key));
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(GetKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove {CacheKey} from Redis; it will expire on its own", GetKey(key));
        }
    }

    public async Task<T> GetOrAddAsync(string key, TimeSpan duration, Func<Task<T>> get)
    {
        var item = await GetAsync(key);
        if (item is not null)
        {
            return item;
        }

        item = await get();
        // Duende passes null for "not found" (e.g. an unknown client_id); don't cache those.
        if (item is not null)
        {
            await SetAsync(key, item, duration);
        }

        return item!;
    }

    // Same idea as Duende's DefaultCache: keys are scoped by type, since the stores reuse
    // plain names (a client id, a scope name) as keys.
    private static string GetKey(string key) => $"{typeof(T).Name}:{key}";
}
