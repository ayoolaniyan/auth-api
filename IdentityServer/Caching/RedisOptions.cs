namespace IdentityServer.Caching;

/// <summary>
/// Connection to the shared Redis instance (the "Redis" configuration section).
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string, e.g. redis:6379.</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Prefix for every key IdentityServer writes, so services sharing Redis don't collide.</summary>
    public string InstanceName { get; set; } = "identityserver:";
}
