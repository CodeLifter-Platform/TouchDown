namespace TD.Services.Telemetry;

/// <summary>
/// Privacy-first telemetry service.
///
/// HARD RULES (non-negotiable):
/// - The service ALWAYS checks <see cref="IsConsentGranted"/> before doing anything.
///   Callers NEVER check consent themselves — the service is the single gatekeeper.
/// - No code content, file paths, repo names, branch names, task text, or agent output
///   may ever be passed to any method on this interface.
/// - Nothing is sent before explicit user consent.
/// - Consent can be revoked at any time from Settings; the service respects it immediately.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// True if the user has granted consent to anonymous telemetry.
    /// Reads live from <see cref="IUserPreferencesService"/> on every access.
    /// </summary>
    bool IsConsentGranted { get; }

    /// <summary>
    /// Track a named event with optional anonymous properties.
    /// Property values must never contain PII, paths, code, or task content.
    /// </summary>
    Task TrackEventAsync(string name, Dictionary<string, object>? props = null);

    /// <summary>
    /// Track an unhandled error. Only the exception type name is recorded —
    /// never the message, stack trace, or any context that could reveal user data.
    /// </summary>
    Task TrackErrorAsync(Exception ex, string component, Dictionary<string, object>? ctx = null);

    /// <summary>
    /// Track the outcome of a completed Drive using a privacy-safe <see cref="DriveResult"/>.
    /// </summary>
    Task TrackDriveOutcomeAsync(DriveResult result);

    /// <summary>
    /// Track a timing measurement for a named operation.
    /// </summary>
    Task TrackTimingAsync(string operation, TimeSpan duration, Dictionary<string, object>? ctx = null);
}

