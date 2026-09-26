using IdentityServerHost.Pages;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IdentityServer.Observability;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Exports traces, metrics and logs over OTLP: incoming requests, outgoing HTTP calls, SQL Server
    /// queries, Redis commands and Duende IdentityServer's own activities and meters. W3C trace context
    /// is read from incoming requests, so a token request made by another service joins its trace.
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
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                // Picks up the IConnectionMultiplexer registered by AddRedisCaching.
                .AddRedisInstrumentation()
                .AddSource("Duende.IdentityServer*"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // Duende's token/login meters and the UI's consent/login counters (Pages/Telemetry.cs).
                .AddMeter("Duende.IdentityServer*", Telemetry.ServiceName))
            .WithLogging(_ => { }, logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            })
            .UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(options.OtlpEndpoint));

        return services;
    }
}
