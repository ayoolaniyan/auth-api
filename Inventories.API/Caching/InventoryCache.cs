using System.Text.Json;
using Inventories.API.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Inventories.API.Caching
{
    /// <summary>
    /// Cache-aside for inventory reads, stored in Redis so every API instance shares it.
    /// </summary>
    /// <remarks>
    /// The cache is optional: if Redis is unavailable the error is logged and the caller
    /// reads from (or writes to) the database as if the entry was not cached.
    /// </remarks>
    public class InventoryCache
    {
        private const string AllKey = "inventories:all";

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IDistributedCache _cache;
        private readonly DistributedCacheEntryOptions _entryOptions;
        private readonly ILogger<InventoryCache> _logger;

        public InventoryCache(IDistributedCache cache, IOptions<RedisOptions> options, ILogger<InventoryCache> logger)
        {
            _cache = cache;
            _entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.InventoryTtlSeconds)
            };
            _logger = logger;
        }

        public Task<List<Inventory>?> GetAllAsync(Func<Task<List<Inventory>?>> load, CancellationToken cancellationToken = default) =>
            GetOrLoadAsync(AllKey, load, cancellationToken);

        public Task<Inventory?> GetAsync(int id, Func<Task<Inventory?>> load, CancellationToken cancellationToken = default) =>
            GetOrLoadAsync(ItemKey(id), load, cancellationToken);

        /// <summary>
        /// Removes the cached list and, when <paramref name="id"/> is given, that item. Call after every write.
        /// </summary>
        public async Task InvalidateAsync(int? id = null, CancellationToken cancellationToken = default)
        {
            await RemoveAsync(AllKey, cancellationToken);
            if (id is not null)
            {
                await RemoveAsync(ItemKey(id.Value), cancellationToken);
            }
        }

        private static string ItemKey(int id) => $"inventories:{id}";

        private async Task<T?> GetOrLoadAsync<T>(string key, Func<Task<T?>> load, CancellationToken cancellationToken) where T : class
        {
            try
            {
                var cached = await _cache.GetAsync(key, cancellationToken);
                if (cached is not null)
                {
                    _logger.LogDebug("Cache hit for {CacheKey}", key);
                    return JsonSerializer.Deserialize<T>(cached, SerializerOptions);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not read {CacheKey} from Redis; loading it from the database", key);
                return await load();
            }

            _logger.LogDebug("Cache miss for {CacheKey}", key);
            var value = await load();

            // Not-found results are not cached, so an item created later is visible immediately.
            if (value is not null)
            {
                try
                {
                    await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions), _entryOptions, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not write {CacheKey} to Redis", key);
                }
            }

            return value;
        }

        private async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The entry stays until its TTL expires, so readers may see stale data until then.
                _logger.LogWarning(ex, "Could not remove {CacheKey} from Redis; it will expire on its own", key);
            }
        }
    }
}
