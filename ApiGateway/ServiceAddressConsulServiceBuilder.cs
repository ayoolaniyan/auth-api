using Consul;
using Ocelot.Logging;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Consul.Interfaces;

namespace ApiGateway;

/// <summary>
/// Routes to the address each service registered with, instead of the Consul node name.
/// </summary>
/// <remarks>
/// Ocelot's default builder uses the node name as the downstream host whenever the entry has a node,
/// and Consul's health endpoint always returns one. With a single Consul agent (docker-compose, kind)
/// that name is the Consul container's hostname, so requests would be sent to Consul itself.
/// </remarks>
public class ServiceAddressConsulServiceBuilder : DefaultConsulServiceBuilder
{
    public ServiceAddressConsulServiceBuilder(IHttpContextAccessor contextAccessor, IConsulClientFactory clientFactory, IOcelotLoggerFactory loggerFactory)
        : base(contextAccessor, clientFactory, loggerFactory)
    {
    }

    protected override string GetDownstreamHost(ServiceEntry entry, Node node) => entry.Service.Address;
}
