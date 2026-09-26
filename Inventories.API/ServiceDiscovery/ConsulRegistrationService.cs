using Consul;
using Microsoft.Extensions.Options;

namespace Inventories.API.ServiceDiscovery
{
    /// <summary>
    /// Registers this instance with Consul on startup and deregisters it on shutdown.
    /// </summary>
    /// <remarks>
    /// Registration is retried in the background rather than blocking startup, because Consul may start
    /// after the API (e.g. in Kubernetes). The registration is also checked periodically and restored if it
    /// disappears, since a dev-mode Consul agent keeps its catalog in memory and loses it on restart.
    /// </remarks>
    public class ConsulRegistrationService : BackgroundService
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

        private readonly IConsulClient _consul;
        private readonly ServiceDiscoveryOptions _options;
        private readonly ILogger<ConsulRegistrationService> _logger;

        public ConsulRegistrationService(IConsulClient consul, IOptions<ServiceDiscoveryOptions> options, ILogger<ConsulRegistrationService> logger)
        {
            _consul = consul;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay;
                try
                {
                    await EnsureRegisteredAsync(stoppingToken);
                    delay = RefreshInterval;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not register {InstanceId} with Consul at {ConsulAddress}, retrying in {Delay}s",
                        _options.InstanceId, _options.ConsulAddress, RetryDelay.TotalSeconds);
                    delay = RetryDelay;
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            try
            {
                await _consul.Agent.ServiceDeregister(_options.InstanceId, cancellationToken);
                _logger.LogInformation("Deregistered {InstanceId} from Consul", _options.InstanceId);
            }
            catch (Exception ex)
            {
                // Consul removes the instance itself once its health check has been failing for a while.
                _logger.LogWarning(ex, "Could not deregister {InstanceId} from Consul", _options.InstanceId);
            }
        }

        private async Task EnsureRegisteredAsync(CancellationToken cancellationToken)
        {
            // Only register when missing: re-registering resets the health check to critical,
            // which would briefly take the instance out of the gateway's rotation.
            var services = await _consul.Agent.Services(cancellationToken);
            if (services.Response.ContainsKey(_options.InstanceId))
            {
                return;
            }

            var healthCheckUrl = _options.ResolvedHealthCheckUrl;
            var registration = new AgentServiceRegistration
            {
                ID = _options.InstanceId,
                Name = _options.ServiceName,
                Address = _options.ServiceAddress,
                Port = _options.ServicePort,
                Check = new AgentServiceCheck
                {
                    HTTP = healthCheckUrl,
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5),
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                    // Locally the API serves the self-signed ASP.NET Core development certificate.
                    TLSSkipVerify = healthCheckUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                }
            };

            await _consul.Agent.ServiceRegister(registration, cancellationToken);
            _logger.LogInformation("Registered {InstanceId} ({Address}:{Port}) with Consul, health check {HealthCheckUrl}",
                _options.InstanceId, _options.ServiceAddress, _options.ServicePort, healthCheckUrl);
        }
    }
}
