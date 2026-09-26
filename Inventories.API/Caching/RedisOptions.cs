namespace Inventories.API.Caching
{
    /// <summary>
    /// Connection to the shared Redis cache (the "Redis" configuration section).
    /// </summary>
    public class RedisOptions
    {
        public const string SectionName = "Redis";

        /// <summary>StackExchange.Redis connection string, e.g. redis:6379.</summary>
        public string ConnectionString { get; set; } = "localhost:6379";

        /// <summary>Prefix for every key this service writes, so services sharing Redis don't collide.</summary>
        public string InstanceName { get; set; } = "inventories-api:";

        /// <summary>How long a cached inventory read is kept before it is loaded from the database again.</summary>
        public int InventoryTtlSeconds { get; set; } = 60;
    }
}
