namespace Inventories.API.Observability
{
    /// <summary>
    /// Where traces, metrics and logs are sent (the "OpenTelemetry" configuration section).
    /// </summary>
    public class ObservabilityOptions
    {
        public const string SectionName = "OpenTelemetry";

        /// <summary>OTLP/gRPC endpoint of the collector, e.g. http://otel-lgtm:4317. Empty turns telemetry off.</summary>
        public string OtlpEndpoint { get; set; } = "";

        /// <summary>service.name attached to every span, metric and log record.</summary>
        public string ServiceName { get; set; } = "inventories-api";
    }
}
