namespace TD.Services.Telemetry;

/// <summary>
/// Diagnostics for agent drives.
///
/// Agent runs are non-deterministic, so a bug report is rarely reproducible and the
/// interesting failure is usually mid-run rather than at the end. This service therefore
/// records full diagnostic detail — repo path, branch, task text, agent output, error
/// messages and stack traces are all in scope.
///
/// Two rules still hold:
/// - The service is the SOLE consent gatekeeper. It checks <see cref="IsConsentGranted"/>
///   itself on every call; callers never check consent before calling.
/// - Nothing is exported before explicit user consent, and consent can be revoked at any
///   time from Settings. Revocation takes effect immediately.
///
/// The exporter endpoint is unset by default, so a stock build records nothing off-machine
/// until one is configured.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// True if the user has granted consent. Read live from <see cref="IUserPreferencesService"/>
    /// on every access so a revocation applies immediately.
    /// </summary>
    bool IsConsentGranted { get; }

    /// <summary>Track a named event with optional properties.</summary>
    Task TrackEventAsync(string name, Dictionary<string, object>? props = null);

    /// <summary>Track an error, including its message and stack trace.</summary>
    Task TrackErrorAsync(Exception ex, string component, Dictionary<string, object>? ctx = null);

    /// <summary>Track the outcome of a completed drive.</summary>
    Task TrackDriveOutcomeAsync(DriveResult result);

    /// <summary>Track a timing measurement for a named operation.</summary>
    Task TrackTimingAsync(string operation, TimeSpan duration, Dictionary<string, object>? ctx = null);

    /// <summary>
    /// Starts a span for a drive. Plays and agent runs nest inside it, so a drive reads as a
    /// waterfall showing wave structure, concurrency, and where time and cost went.
    /// Returns a disposable scope; disposing ends the span. Never returns null.
    /// </summary>
    ITelemetryScope StartDriveScope(string driveId, Dictionary<string, object>? attributes = null);

    /// <summary>Starts a span for one play, nested under the active drive span.</summary>
    ITelemetryScope StartPlayScope(string playName, Dictionary<string, object>? attributes = null);

    /// <summary>Starts a span for a single agent invocation, nested under the active play span.</summary>
    ITelemetryScope StartAgentScope(string agentName, Dictionary<string, object>? attributes = null);
}

/// <summary>
/// An open span. Disposing ends it. Implementations must tolerate being disposed more than
/// once and must never throw — telemetry failing is never a reason for a drive to fail.
/// </summary>
public interface ITelemetryScope : IDisposable
{
    /// <summary>Adds or overwrites an attribute on the span.</summary>
    void SetAttribute(string key, object? value);

    /// <summary>Records a point-in-time event on the span.</summary>
    void AddEvent(string name, Dictionary<string, object>? attributes = null);

    /// <summary>Marks the span as failed and attaches the exception.</summary>
    void SetError(Exception ex);

    /// <summary>Marks the span as failed with a description, when there is no exception.</summary>
    void SetError(string description);
}
