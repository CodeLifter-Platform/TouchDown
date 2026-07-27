using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TD.Services.Telemetry;

/// <summary>DI wiring for telemetry.</summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITelemetryService"/> and, when an OTLP endpoint is configured,
    /// the exporter behind it.
    ///
    /// The endpoint is deliberately unset by default: a stock build records spans in-process
    /// but ships nothing anywhere until <c>Telemetry:OtlpEndpoint</c> (or the standard
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>) is set. Export is additionally gated on the user
    /// having granted consent, which <see cref="OpenTelemetryService"/> enforces before a
    /// span is ever started.
    /// </summary>
    public static IServiceCollection AddTouchDownTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Singleton: the session id is per app launch, and the singleton orchestrator needs it.
        services.AddSingleton<ITelemetryService, OpenTelemetryService>();

        var endpoint = configuration["Telemetry:OtlpEndpoint"]
                       ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (string.IsNullOrWhiteSpace(endpoint))
            return services;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
            throw new InvalidOperationException(
                $"Telemetry:OtlpEndpoint is not a valid absolute URI: '{endpoint}'");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: configuration["Telemetry:ServiceName"] ?? "touchdown",
                serviceVersion: typeof(TelemetryServiceCollectionExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing => tracing
                .AddSource(TelemetryActivitySource.Name)
                .AddOtlpExporter(options => options.Endpoint = endpointUri));

        return services;
    }
}
