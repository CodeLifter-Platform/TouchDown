using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using TD.Services;
using TD.Services.Telemetry;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Configuration;

/// <summary>
/// Telemetry export is off until an OTLP endpoint is configured. A misconfigured endpoint
/// must refuse to start the app with a message that names the setting, rather than start
/// and silently export nowhere.
/// </summary>
public class TelemetryStartupGuardTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

    private static ServiceProvider Build(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUserPreferencesService>(new StubPreferences());
        services.AddTouchDownTelemetry(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void An_invalid_endpoint_refuses_to_start_and_names_the_setting()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(Config(("Telemetry:OtlpEndpoint", "not a uri"))));

        Assert.Contains("Telemetry:OtlpEndpoint", ex.Message);
        Assert.Contains("not a uri", ex.Message);
    }

    [Fact]
    public void A_relative_endpoint_is_rejected_too()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(Config(("Telemetry:OtlpEndpoint", "/collector"))));

        Assert.Contains("Telemetry:OtlpEndpoint", ex.Message);
    }

    [Fact]
    public void No_endpoint_means_spans_stay_in_process_and_nothing_is_exported()
    {
        using var provider = Build(Config());

        Assert.NotNull(provider.GetRequiredService<ITelemetryService>());
        Assert.Null(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void A_valid_endpoint_wires_the_exporter()
    {
        using var provider = Build(Config(("Telemetry:OtlpEndpoint", "http://collector.local:4317")));

        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void The_standard_otel_variable_is_honoured_when_the_app_setting_is_absent()
    {
        using var provider = Build(Config(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector.local:4318")));

        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void The_hosted_app_will_not_boot_with_an_invalid_endpoint()
    {
        using var app = new HostedApp(settings: new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEndpoint"] = "definitely not a uri",
        });

        var ex = Assert.ThrowsAny<Exception>(() => app.Services);

        var messages = string.Join(" | ", Unwrap(ex).Select(e => e.Message));
        Assert.Contains("Telemetry:OtlpEndpoint", messages);
    }

    private static IEnumerable<Exception> Unwrap(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException) yield return e;
    }
}
