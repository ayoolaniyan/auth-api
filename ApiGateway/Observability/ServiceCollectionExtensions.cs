using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiGateway.Observability;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Exports traces, metrics and logs over OTLP: incoming requests and the calls Ocelot makes to Consul
    /// and to downstream services. The W3C traceparent header is forwarded, so a request keeps one trace
    /// from the client through the gateway to the API. Metrics include the rate limiter's.
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
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
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
