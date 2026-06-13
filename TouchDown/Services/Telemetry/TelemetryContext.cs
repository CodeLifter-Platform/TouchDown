using TD.Models;

namespace TD.Services.Telemetry;

/// <summary>
/// Shared anonymous context bag attached to telemetry events.
/// HARD RULE: Must never contain code, file paths, repo names, task text, or agent output.
/// All identifiers here are opaque UUIDs or numeric counts.
/// </summary>
public record TelemetryContext
{
    /// <summary>Opaque drive identifier (short random ID — never a path or name).</summary>
    public string? DriveId { get; init; }

    /// <summary>Opaque session identifier generated per app launch.</summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Application version string (e.g. "1.0.0").</summary>
    public string AppVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Privacy-safe summary of a Drive outcome used by <see cref="ITelemetryService.TrackDriveOutcomeAsync"/>.
/// Contains only anonymous numeric/enum data — no paths, names, or content.
/// </summary>
public record DriveResult
{
    /// <summary>Opaque drive identifier.</summary>
    public required string DriveId { get; init; }

    /// <summary>Final status of the drive.</summary>
    public required DriveStatus Status { get; init; }

    /// <summary>Number of plays that were executed.</summary>
    public int PlayCount { get; init; }

    /// <summary>Number of plays that failed.</summary>
    public int FailedPlayCount { get; init; }

    /// <summary>Total drive duration in milliseconds.</summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Type name of the exception if the drive failed, e.g. "TimeoutException".
    /// NEVER include the exception message or stack trace.
    /// </summary>
    public string? ErrorType { get; init; }

    /// <summary>The workspace mode used (FreshFolder, PrWorktree, CurrentBranch).</summary>
    public WorkspaceMode WorkspaceMode { get; init; }

    /// <summary>Maximum agent parallelism configured.</summary>
    public int MaxParallelism { get; init; }
}

