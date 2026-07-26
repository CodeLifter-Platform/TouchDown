using System.Diagnostics;

namespace TD.Services.Telemetry;

/// <summary>
/// The single <see cref="ActivitySource"/> TouchDown emits spans from. OpenTelemetry
/// subscribes to it by name, so both the source and the name live here.
/// </summary>
public static class TelemetryActivitySource
{
    public const string Name = "TouchDown";

    public static ActivitySource Instance { get; } = new(Name);
}
