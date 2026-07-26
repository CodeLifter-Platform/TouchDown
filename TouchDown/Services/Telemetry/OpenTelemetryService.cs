using System.Diagnostics;

namespace TD.Services.Telemetry;

/// <summary>
/// <see cref="ITelemetryService"/> backed by OpenTelemetry spans.
///
/// A drive is emitted as a trace — drive → play → agent run — so wave structure and
/// concurrency show up directly in a waterfall instead of having to be reconstructed from
/// counters. Point-in-time events are recorded on whichever span is active.
///
/// Consent is checked here on every call, so callers never have to. With consent withheld
/// no span is started and no event recorded, which means nothing reaches the exporter.
/// </summary>
public class OpenTelemetryService : ITelemetryService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogger<OpenTelemetryService> _logger;
    private readonly TelemetryContext _context = new();

    public OpenTelemetryService(IUserPreferencesService prefs, ILogger<OpenTelemetryService> logger)
    {
        _prefs = prefs;
        _logger = logger;
    }

    public bool IsConsentGranted
    {
        get
        {
            try
            {
                return _prefs.Current.TelemetryConsented;
            }
            catch (Exception ex)
            {
                // Failing to read preferences must not be treated as consent.
                _logger.LogDebug(ex, "Could not read telemetry consent; treating as not granted");
                return false;
            }
        }
    }

    public Task TrackEventAsync(string name, Dictionary<string, object>? props = null)
    {
        if (!IsConsentGranted) return Task.CompletedTask;

        try
        {
            var activity = Activity.Current;
            if (activity is not null)
            {
                activity.AddEvent(new ActivityEvent(name, tags: ToTags(props)));
            }
            else
            {
                // No enclosing span (e.g. a wizard interaction) — emit a standalone one.
                using var standalone = TelemetryActivitySource.Instance.StartActivity(name, ActivityKind.Internal);
                ApplyAttributes(standalone, props);
                ApplyContext(standalone);
            }
        }
        catch (Exception ex)
        {
            LogSwallowed(ex, name);
        }

        return Task.CompletedTask;
    }

    public Task TrackErrorAsync(Exception ex, string component, Dictionary<string, object>? ctx = null)
    {
        if (!IsConsentGranted) return Task.CompletedTask;

        try
        {
            var props = ctx is null ? [] : new Dictionary<string, object>(ctx);
            props[TelemetryAttribute.Component] = component;
            props[TelemetryAttribute.ErrorType] = ex.GetType().Name;
            props[TelemetryAttribute.ErrorMessage] = ex.Message;
            if (ex.StackTrace is { } stack) props[TelemetryAttribute.ErrorStack] = stack;

            var activity = Activity.Current;
            if (activity is not null)
            {
                activity.AddEvent(new ActivityEvent(TelemetryEvent.ErrorUnhandled, tags: ToTags(props)));
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            else
            {
                using var standalone = TelemetryActivitySource.Instance.StartActivity(
                    TelemetryEvent.ErrorUnhandled, ActivityKind.Internal);
                ApplyAttributes(standalone, props);
                ApplyContext(standalone);
                standalone?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }
        catch (Exception inner)
        {
            LogSwallowed(inner, TelemetryEvent.ErrorUnhandled);
        }

        return Task.CompletedTask;
    }

    public Task TrackDriveOutcomeAsync(DriveResult result)
    {
        var props = new Dictionary<string, object>
        {
            [TelemetryAttribute.DriveId] = result.DriveId,
            [TelemetryAttribute.DriveStatus] = result.Status.ToString(),
            [TelemetryAttribute.PlayCount] = result.PlayCount,
            ["td.drive.failed_play_count"] = result.FailedPlayCount,
            [TelemetryAttribute.WorkspaceMode] = result.WorkspaceMode.ToString(),
            [TelemetryAttribute.MaxParallelism] = result.MaxParallelism,
        };

        if (result.DurationMs is { } duration) props[TelemetryAttribute.DurationMs] = duration;
        if (result.CostUsd is { } cost) props[TelemetryAttribute.CostUsd] = cost;
        if (result.ErrorType is { } errorType) props[TelemetryAttribute.ErrorType] = errorType;
        if (result.ErrorMessage is { } errorMessage) props[TelemetryAttribute.ErrorMessage] = errorMessage;
        if (result.TurnoverReason is { } reason) props["td.drive.turnover_reason"] = reason.ToString();
        if (result.PlanSource is { } planSource) props[TelemetryAttribute.PlanSource] = planSource.ToString();
        if (result.ProviderId is { } provider) props[TelemetryAttribute.ProviderId] = provider;
        if (result.ModelId is { } model) props[TelemetryAttribute.ModelId] = model;

        var name = result.Status switch
        {
            Models.DriveStatus.Touchdown => TelemetryEvent.DriveTouchdown,
            Models.DriveStatus.Turnover => TelemetryEvent.DriveTurnover,
            _ => TelemetryEvent.DriveTurnover
        };

        return TrackEventAsync(name, props);
    }

    public Task TrackTimingAsync(string operation, TimeSpan duration, Dictionary<string, object>? ctx = null)
    {
        var props = ctx is null ? [] : new Dictionary<string, object>(ctx);
        props[TelemetryAttribute.DurationMs] = (long)duration.TotalMilliseconds;
        return TrackEventAsync(operation, props);
    }

    // ── Spans ────────────────────────────────────────────────────────────────

    public ITelemetryScope StartDriveScope(string driveId, Dictionary<string, object>? attributes = null)
    {
        var props = attributes is null ? [] : new Dictionary<string, object>(attributes);
        props[TelemetryAttribute.DriveId] = driveId;
        return Start($"drive {driveId}", props);
    }

    public ITelemetryScope StartPlayScope(string playName, Dictionary<string, object>? attributes = null) =>
        Start($"play {playName}", attributes);

    public ITelemetryScope StartAgentScope(string agentName, Dictionary<string, object>? attributes = null) =>
        Start($"agent {agentName}", attributes);

    private ITelemetryScope Start(string name, Dictionary<string, object>? attributes)
    {
        if (!IsConsentGranted) return NullTelemetryScope.Instance;

        try
        {
            var activity = TelemetryActivitySource.Instance.StartActivity(name, ActivityKind.Internal);
            if (activity is null) return NullTelemetryScope.Instance;

            ApplyAttributes(activity, attributes);
            ApplyContext(activity);
            return new ActivityTelemetryScope(activity);
        }
        catch (Exception ex)
        {
            LogSwallowed(ex, name);
            return NullTelemetryScope.Instance;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ApplyContext(Activity? activity)
    {
        activity?.SetTag("td.session.id", _context.SessionId);
        activity?.SetTag("td.app.version", _context.AppVersion);
    }

    private static void ApplyAttributes(Activity? activity, Dictionary<string, object>? props)
    {
        if (activity is null || props is null) return;
        foreach (var (key, value) in props) activity.SetTag(key, value);
    }

    private static ActivityTagsCollection? ToTags(Dictionary<string, object>? props)
    {
        if (props is null) return null;
        var tags = new ActivityTagsCollection();
        foreach (var (key, value) in props) tags[key] = value;
        return tags;
    }

    private void LogSwallowed(Exception ex, string what) =>
        // Telemetry failing must never surface to the user or break a drive.
        _logger.LogDebug(ex, "Telemetry call {What} failed and was ignored", what);
}

/// <summary>Wraps an <see cref="Activity"/> so callers depend on the interface, not on OTel types.</summary>
internal sealed class ActivityTelemetryScope : ITelemetryScope
{
    private readonly Activity _activity;
    private bool _disposed;

    public ActivityTelemetryScope(Activity activity) => _activity = activity;

    public void SetAttribute(string key, object? value)
    {
        try { _activity.SetTag(key, value); } catch (Exception) { /* never throw from telemetry */ }
    }

    public void AddEvent(string name, Dictionary<string, object>? attributes = null)
    {
        try
        {
            ActivityTagsCollection? tags = null;
            if (attributes is not null)
            {
                tags = [];
                foreach (var (key, value) in attributes) tags[key] = value;
            }
            _activity.AddEvent(new ActivityEvent(name, tags: tags));
        }
        catch (Exception) { /* never throw from telemetry */ }
    }

    public void SetError(Exception ex)
    {
        try
        {
            _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _activity.SetTag(TelemetryAttribute.ErrorType, ex.GetType().Name);
            _activity.SetTag(TelemetryAttribute.ErrorMessage, ex.Message);
            if (ex.StackTrace is { } stack) _activity.SetTag(TelemetryAttribute.ErrorStack, stack);
        }
        catch (Exception) { /* never throw from telemetry */ }
    }

    public void SetError(string description)
    {
        try { _activity.SetStatus(ActivityStatusCode.Error, description); } catch (Exception) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _activity.Dispose(); } catch (Exception) { /* never throw from telemetry */ }
    }
}

/// <summary>The scope handed out when consent is withheld or no listener is sampling. Does nothing.</summary>
internal sealed class NullTelemetryScope : ITelemetryScope
{
    public static readonly NullTelemetryScope Instance = new();

    public void SetAttribute(string key, object? value) { }
    public void AddEvent(string name, Dictionary<string, object>? attributes = null) { }
    public void SetError(Exception ex) { }
    public void SetError(string description) { }
    public void Dispose() { }
}
