using Consul;
using Microsoft.Extensions.Options;

namespace Inventories.API.ServiceDiscovery
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers this instance with Consul so the API Gateway can discover it.
        /// </summary>
        public static IServiceCollection AddConsulServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ServiceDiscoveryOptions>(configuration.GetSection(ServiceDiscoveryOptions.SectionName));

            services.AddSingleton<IConsulClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
                return new ConsulClient(config => config.Address = new Uri(options.ConsulAddress));
            });

            services.AddHostedService<ConsulRegistrationService>();

            return services;
        }
    }
}
