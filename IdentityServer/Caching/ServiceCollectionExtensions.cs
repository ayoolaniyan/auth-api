using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace IdentityServer.Caching;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Uses Redis for the IdentityServer configuration store cache and for ASP.NET Data Protection keys,
    /// so both are shared across instances and survive restarts.
    /// </summary>
    /// <remarks>Call together with <c>AddConfigurationStoreCache()</c> on the IdentityServer builder.</remarks>
    public static IServiceCollection AddRedisCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        var redisConfiguration = ConfigurationOptions.Parse(options.ConnectionString);
        // Start even if Redis is down and keep reconnecting in the background.
        redisConfiguration.AbortOnConnectFail = false;
        // While disconnected, fail cache calls immediately so requests fall back to the database.
        redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
        var redis = ConnectionMultiplexer.Connect(redisConfiguration);
        services.AddSingleton<IConnectionMultiplexer>(redis);

        services.AddStackExchangeRedisCache(cache =>
        {
            cache.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redis);
            cache.InstanceName = options.InstanceName;
        });

        // The configuration store cache types. These closed registrations take precedence over the
        // open-generic in-memory DefaultCache<> that AddConfigurationStoreCache() registers; any other
        // cached type (e.g. CORS origins) stays in memory.
        services.AddSingleton<ICache<Client>, DistributedCache<Client>>();
        services.AddSingleton<ICache<IdentityResource>, DistributedCache<IdentityResource>>();
        services.AddSingleton<ICache<ApiResource>, DistributedCache<ApiResource>>();
        services.AddSingleton<ICache<ApiScope>, DistributedCache<ApiScope>>();
        services.AddSingleton<ICache<Resources>, DistributedCache<Resources>>();

        // Keys that protect cookies and anti-forgery tokens. Stored in Redis so a restarted or
        // second instance can read cookies issued by another one.
        services.AddDataProtection()
            .SetApplicationName("identityserver")
            .PersistKeysToStackExchangeRedis(redis, $"{options.InstanceName}data-protection-keys");

        return services;
    }
}
