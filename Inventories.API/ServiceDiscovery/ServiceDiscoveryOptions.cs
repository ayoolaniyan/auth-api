namespace Inventories.API.ServiceDiscovery
{
    /// <summary>
    /// How this instance registers itself with Consul (the "ServiceDiscovery" configuration section).
    /// </summary>
    public class ServiceDiscoveryOptions
    {
        public const string SectionName = "ServiceDiscovery";

        /// <summary>Consul agent HTTP API, e.g. http://consul:8500.</summary>
        public string ConsulAddress { get; set; } = "http://localhost:8500";

        /// <summary>Name the API Gateway looks up (ServiceName in ocelot.json).</summary>
        public string ServiceName { get; set; } = "inventories-api";

        /// <summary>Host the gateway should call: a hostname or IP, without scheme.</summary>
        public string ServiceAddress { get; set; } = "localhost";

        public int ServicePort { get; set; } = 8080;

        /// <summary>
        /// URL Consul polls to check this instance. Defaults to http://{ServiceAddress}:{ServicePort}/health;
        /// set it when Consul cannot reach the service at that address (e.g. Consul in Docker, service on the host).
        /// </summary>
        public string? HealthCheckUrl { get; set; }

        /// <summary>Unique per instance, so several replicas can register under the same ServiceName.</summary>
        public string InstanceId => $"{ServiceName}-{ServiceAddress}-{ServicePort}";

        public string ResolvedHealthCheckUrl => string.IsNullOrWhiteSpace(HealthCheckUrl)
            ? $"http://{ServiceAddress}:{ServicePort}/health"
            : HealthCheckUrl;
    }
}
