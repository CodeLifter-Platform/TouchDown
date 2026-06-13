namespace TD.Services.Telemetry;

/// <summary>
/// Null Object implementation of <see cref="ITelemetryService"/>.
/// Every method returns <see cref="Task.CompletedTask"/> — no data is ever sent anywhere.
///
/// This is the ONLY implementation in v1. When a real backend is chosen:
///   1. Add one new class implementing <see cref="ITelemetryService"/>.
///   2. Swap the registration in Program.cs.
///   3. Zero other changes required.
///
/// Consent is checked on every call. The service is the sole gatekeeper —
/// callers must never check consent themselves.
/// </summary>
public class NullTelemetryService : ITelemetryService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogger<NullTelemetryService> _logger;

    public NullTelemetryService(IUserPreferencesService prefs, ILogger<NullTelemetryService> logger)
    {
        _prefs = prefs;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConsentGranted => _prefs.Current.TelemetryConsented;

    /// <inheritdoc />
    public Task TrackEventAsync(string name, Dictionary<string, object>? props = null)
    {
        if (!IsConsentGranted) return Task.CompletedTask;
        _logger.LogDebug("[Telemetry/Null] Event: {Name}", name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TrackErrorAsync(Exception ex, string component, Dictionary<string, object>? ctx = null)
    {
        if (!IsConsentGranted) return Task.CompletedTask;
        // Log only the type name — never message or stack trace (HARD RULE)
        _logger.LogDebug("[Telemetry/Null] Error: {ExType} in {Component}", ex.GetType().Name, component);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TrackDriveOutcomeAsync(DriveResult result)
    {
        if (!IsConsentGranted) return Task.CompletedTask;
        _logger.LogDebug("[Telemetry/Null] DriveOutcome: {DriveId} → {Status}", result.DriveId, result.Status);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TrackTimingAsync(string operation, TimeSpan duration, Dictionary<string, object>? ctx = null)
    {
        if (!IsConsentGranted) return Task.CompletedTask;
        _logger.LogDebug("[Telemetry/Null] Timing: {Op} = {Ms}ms", operation, (long)duration.TotalMilliseconds);
        return Task.CompletedTask;
    }
}

