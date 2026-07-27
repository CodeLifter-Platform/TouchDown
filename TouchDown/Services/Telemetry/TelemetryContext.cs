using TD.Models;

namespace TD.Services.Telemetry;

/// <summary>Shared context attached to every telemetry event.</summary>
public record TelemetryContext
{
    /// <summary>Drive identifier (the short public id, matching the URL).</summary>
    public string? DriveId { get; init; }

    /// <summary>Session identifier generated per app launch.</summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Application version string.</summary>
    public string AppVersion { get; init; } =
        typeof(TelemetryContext).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

/// <summary>Summary of a drive outcome for <see cref="ITelemetryService.TrackDriveOutcomeAsync"/>.</summary>
public record DriveResult
{
    public required string DriveId { get; init; }

    public required DriveStatus Status { get; init; }

    /// <summary>Number of plays that were executed.</summary>
    public int PlayCount { get; init; }

    /// <summary>Number of plays that failed.</summary>
    public int FailedPlayCount { get; init; }

    /// <summary>Total drive duration in milliseconds.</summary>
    public long? DurationMs { get; init; }

    /// <summary>Total cost of the drive in USD, when the provider reported one.</summary>
    public double? CostUsd { get; init; }

    /// <summary>Exception type name if the drive failed.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Exception message if the drive failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Why the drive ended as it did. Every failure previously collapsed into the single
    /// Turnover status, which said nothing about what actually went wrong.
    /// </summary>
    public TurnoverReason? TurnoverReason { get; init; }

    public WorkspaceMode WorkspaceMode { get; init; }

    public int MaxParallelism { get; init; }

    /// <summary>How the plan for this drive was obtained.</summary>
    public PlanSource? PlanSource { get; init; }

    /// <summary>The provider that ran this drive.</summary>
    public string? ProviderId { get; init; }

    /// <summary>The primary model used.</summary>
    public string? ModelId { get; init; }
}

/// <summary>A classification of why a drive did not reach a Touchdown.</summary>
public enum TurnoverReason
{
    /// <summary>The user cancelled the drive.</summary>
    Cancelled,

    /// <summary>The workspace could not be prepared (clone, worktree or folder creation failed).</summary>
    WorkspaceSetupFailed,

    /// <summary>No usable plan could be produced.</summary>
    PlanningFailed,

    /// <summary>One or more plays failed during execution.</summary>
    PlayFailed,

    /// <summary>The selected agent provider was unavailable or not authenticated.</summary>
    ProviderUnavailable,

    /// <summary>The app restarted while the drive was running.</summary>
    Orphaned,

    /// <summary>Anything not covered above.</summary>
    Unknown
}
