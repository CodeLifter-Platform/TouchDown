using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TD.Models;
using TD.Services;
using TD.Services.Telemetry;

namespace TouchDown.Tests.Services;

/// <summary>
/// The telemetry service is the sole consent gatekeeper — callers never check consent
/// themselves — so everything hinges on it refusing to record when consent is absent.
/// It must also never throw: telemetry failing is not a reason for a drive to fail.
/// </summary>
public class TelemetryServiceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _started = [];
    private readonly List<Activity> _stopped = [];

    public TelemetryServiceTests()
    {
        // Without a listener that samples, ActivitySource.StartActivity returns null and
        // nothing would be recorded regardless of consent — so subscribe first.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _started.Add(activity),
            ActivityStopped = activity => _stopped.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private static OpenTelemetryService CreateService(bool consented)
    {
        var prefs = new StubPreferences { Current = new UserPreferences { TelemetryConsented = consented } };
        return new OpenTelemetryService(prefs, NullLogger<OpenTelemetryService>.Instance);
    }

    // ── Consent gating ───────────────────────────────────────────────────────

    [Fact]
    public void ConsentGranted_IsReadLiveFromPreferences()
    {
        var prefs = new StubPreferences { Current = new UserPreferences { TelemetryConsented = false } };
        var telemetry = new OpenTelemetryService(prefs, NullLogger<OpenTelemetryService>.Instance);
        Assert.False(telemetry.IsConsentGranted);

        // Revoking or granting must take effect immediately, not at next restart.
        prefs.Current = new UserPreferences { TelemetryConsented = true };

        Assert.True(telemetry.IsConsentGranted);
    }

    [Fact]
    public void WithoutConsent_NoDriveSpanIsStarted()
    {
        var telemetry = CreateService(consented: false);

        using var scope = telemetry.StartDriveScope("drive123");

        Assert.Empty(_started);
    }

    [Fact]
    public async Task WithoutConsent_NoEventIsRecorded()
    {
        var telemetry = CreateService(consented: false);

        await telemetry.TrackEventAsync(TelemetryEvent.DriveStarted);
        await telemetry.TrackErrorAsync(new InvalidOperationException("boom"), "test");
        await telemetry.TrackTimingAsync("op", TimeSpan.FromSeconds(1));

        Assert.Empty(_started);
    }

    [Fact]
    public void UnreadablePreferences_AreTreatedAsNoConsent()
    {
        // Failing open would export data the user never agreed to.
        var telemetry = new OpenTelemetryService(new ThrowingPreferences(), NullLogger<OpenTelemetryService>.Instance);

        Assert.False(telemetry.IsConsentGranted);
        using var scope = telemetry.StartDriveScope("drive123");
        Assert.Empty(_started);
    }

    // ── Span shape ───────────────────────────────────────────────────────────

    [Fact]
    public void DriveScope_StartsASpanCarryingTheDriveId()
    {
        var telemetry = CreateService(consented: true);

        using var scope = telemetry.StartDriveScope("drive123");

        var activity = Assert.Single(_started);
        Assert.Contains("drive123", activity.DisplayName);
        Assert.Equal("drive123", activity.GetTagItem(TelemetryAttribute.DriveId));
    }

    [Fact]
    public void PlayAndAgentScopes_NestInsideTheDriveSpan()
    {
        // The nesting is what makes a drive read as a waterfall.
        var telemetry = CreateService(consented: true);

        using var drive = telemetry.StartDriveScope("drive123");
        using var play = telemetry.StartPlayScope("The Offensive Line #1");
        using var agent = telemetry.StartAgentScope("The Offensive Line #1");

        Assert.Equal(3, _started.Count);
        Assert.Null(_started[0].Parent);
        Assert.Same(_started[0], _started[1].Parent);
        Assert.Same(_started[1], _started[2].Parent);
    }

    [Fact]
    public void ScopeAttributes_LandOnTheSpan()
    {
        var telemetry = CreateService(consented: true);

        using var scope = telemetry.StartDriveScope("d1", new Dictionary<string, object>
        {
            [TelemetryAttribute.TaskDescription] = "add a login page",
            [TelemetryAttribute.MaxParallelism] = 4,
        });

        var activity = Assert.Single(_started);
        Assert.Equal("add a login page", activity.GetTagItem(TelemetryAttribute.TaskDescription));
        Assert.Equal(4, activity.GetTagItem(TelemetryAttribute.MaxParallelism));
    }

    [Fact]
    public void RichContent_IsRecorded()
    {
        // Diagnostic detail is wanted: repo path, branch and task text are all in scope.
        var telemetry = CreateService(consented: true);

        using var scope = telemetry.StartDriveScope("d1", new Dictionary<string, object>
        {
            [TelemetryAttribute.RepoPath] = "/home/dev/project",
            [TelemetryAttribute.Branch] = "feature/login",
        });

        var activity = Assert.Single(_started);
        Assert.Equal("/home/dev/project", activity.GetTagItem(TelemetryAttribute.RepoPath));
        Assert.Equal("feature/login", activity.GetTagItem(TelemetryAttribute.Branch));
    }

    [Fact]
    public void SetError_MarksTheSpanFailedAndKeepsTheStackTrace()
    {
        var telemetry = CreateService(consented: true);
        Exception caught;
        try { throw new InvalidOperationException("agent exploded"); }
        catch (Exception ex) { caught = ex; }

        using (var scope = telemetry.StartDriveScope("d1"))
        {
            scope.SetError(caught);
        }

        var activity = Assert.Single(_stopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("InvalidOperationException", activity.GetTagItem(TelemetryAttribute.ErrorType));
        Assert.Equal("agent exploded", activity.GetTagItem(TelemetryAttribute.ErrorMessage));
        Assert.NotNull(activity.GetTagItem(TelemetryAttribute.ErrorStack));
    }

    [Fact]
    public async Task EventsInsideAScope_AttachToThatSpan()
    {
        var telemetry = CreateService(consented: true);

        using (var scope = telemetry.StartDriveScope("d1"))
        {
            await telemetry.TrackEventAsync(TelemetryEvent.PlanResolved, new Dictionary<string, object>
            {
                [TelemetryAttribute.PlanSource] = nameof(PlanSource.Fallback),
            });
        }

        var activity = Assert.Single(_stopped);
        var evt = Assert.Single(activity.Events);
        Assert.Equal(TelemetryEvent.PlanResolved, evt.Name);
        Assert.Contains(evt.Tags, t => t.Key == TelemetryAttribute.PlanSource
                                       && (string?)t.Value == nameof(PlanSource.Fallback));
    }

    [Fact]
    public async Task DriveOutcome_CarriesTurnoverReasonAndPlanSource()
    {
        // Every failure used to collapse into the bare Turnover status.
        var telemetry = CreateService(consented: true);

        await telemetry.TrackDriveOutcomeAsync(new DriveResult
        {
            DriveId = "d1",
            Status = DriveStatus.Turnover,
            PlayCount = 3,
            FailedPlayCount = 1,
            TurnoverReason = TurnoverReason.PlanningFailed,
            PlanSource = PlanSource.Fallback,
            CostUsd = 0.42,
        });

        var activity = Assert.Single(_stopped);
        Assert.Equal(TelemetryEvent.DriveTurnover, activity.DisplayName);
        Assert.Equal(nameof(TurnoverReason.PlanningFailed), activity.GetTagItem("td.drive.turnover_reason"));
        Assert.Equal(nameof(PlanSource.Fallback), activity.GetTagItem(TelemetryAttribute.PlanSource));
        Assert.Equal(0.42, activity.GetTagItem(TelemetryAttribute.CostUsd));
    }

    [Fact]
    public async Task TimingEvent_RecordsDurationInMilliseconds()
    {
        var telemetry = CreateService(consented: true);

        await telemetry.TrackTimingAsync(TelemetryEvent.PerfWorktreeSetup, TimeSpan.FromMilliseconds(1500));

        var activity = Assert.Single(_stopped);
        Assert.Equal(1500L, activity.GetTagItem(TelemetryAttribute.DurationMs));
    }

    // ── Robustness ───────────────────────────────────────────────────────────

    [Fact]
    public void DisposingAScopeTwice_IsHarmless()
    {
        var telemetry = CreateService(consented: true);
        var scope = telemetry.StartDriveScope("d1");

        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void ScopeCallsAfterDisposal_DoNotThrow()
    {
        // A drive must never fail because telemetry did.
        var telemetry = CreateService(consented: true);
        var scope = telemetry.StartDriveScope("d1");
        scope.Dispose();

        scope.SetAttribute("k", "v");
        scope.AddEvent("late");
        scope.SetError("late");
    }

    [Fact]
    public void ScopeIsNeverNull_EvenWithoutConsent()
    {
        var telemetry = CreateService(consented: false);

        Assert.NotNull(telemetry.StartDriveScope("d1"));
        Assert.NotNull(telemetry.StartPlayScope("p"));
        Assert.NotNull(telemetry.StartAgentScope("a"));
    }

    private sealed class StubPreferences : IUserPreferencesService
    {
        public UserPreferences Current { get; set; } = new();
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ThrowingPreferences : IUserPreferencesService
    {
        public UserPreferences Current => throw new InvalidOperationException("preferences unavailable");
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
