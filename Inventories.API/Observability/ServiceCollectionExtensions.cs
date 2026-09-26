using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Inventories.API.Observability
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Exports traces, metrics and logs over OTLP: incoming requests, outgoing HTTP calls (IdentityServer
        /// discovery, Consul) and Redis commands. W3C trace context is read from incoming requests, so calls
        /// routed through the API Gateway continue the caller's trace.
        /// </summary>
        /// <remarks>Does nothing when no OTLP endpoint is configured, so the service runs without a collector.</remarks>
        public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
                ?? new ObservabilityOptions();
            if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                return services;
            }

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(options.ServiceName, serviceVersion: typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString())
                    .AddAttributes([new("deployment.environment.name", environment.EnvironmentName)]))
                .WithTracing(tracing => tracing
                    // Consul and Kubernetes poll /health every few seconds; those spans would bury real requests.
                    .AddAspNetCoreInstrumentation(instrumentation =>
                        instrumentation.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    // Picks up the IConnectionMultiplexer registered by AddRedisDistributedCache.
                    .AddRedisInstrumentation())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation())
                .WithLogging(_ => { }, logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                })
                .UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(options.OtlpEndpoint));

            return services;
        }
    }
}
