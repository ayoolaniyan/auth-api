using StackExchange.Redis;

namespace Inventories.API.Caching
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a Redis-backed <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
        /// shared by every instance of the API, and the <see cref="InventoryCache"/> built on it.
        /// </summary>
        public static IServiceCollection AddRedisDistributedCache(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(RedisOptions.SectionName);
            services.Configure<RedisOptions>(section);
            var options = section.Get<RedisOptions>() ?? new RedisOptions();

            var redisConfiguration = ConfigurationOptions.Parse(options.ConnectionString);
            // Start even if Redis is down and keep reconnecting in the background.
            redisConfiguration.AbortOnConnectFail = false;
            // While disconnected, fail cache calls immediately instead of queueing them,
            // so requests fall back to the database without waiting for a timeout.
            redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;

            // Registered so OpenTelemetry can trace its commands (see Observability/).
            var redis = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(redis);

            services.AddStackExchangeRedisCache(cache =>
            {
                cache.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redis);
                cache.InstanceName = options.InstanceName;
            });

            services.AddSingleton<InventoryCache>();

            return services;
        }
    }
}
