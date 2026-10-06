using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TD.Areas.Drives.New;
using TD.Models;
using TD.Services;
using TD.Services.Telemetry;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// What a drive does when something under it fails. Execution runs on a background task, so
/// each test polls the database for the terminal state and then asserts on what was
/// persisted and what a monitoring client was sent. Every dependency except the model
/// provider and git is the real implementation.
/// </summary>
public class OrchestratorFailurePathTests
{
    private const string PlanJson = """
        {"summary":"Ship it","assignments":[
          {"agent_role":"Worker","agent_name":"The Offensive Line","task":"build it","depends_on":[],"priority":1},
          {"agent_role":"Validator","agent_name":"The Safety","task":"review it","depends_on":[0],"priority":2}
        ]}
        """;

    private sealed class Harness : IDisposable
    {
        public TestDb Db { get; } = TestDb.CreateOnDisk();
        public RecordingHubContext Hub { get; } = new();
        public RecordingTelemetry Telemetry { get; } = new();
        public TempRepo Repo { get; } = TempRepo.CreateGitRepo().WithCommit();

        public AgentOrchestrationService Orchestrator(IAgentProvider provider, IGitWorktreeService? git = null) =>
            new(Db,
                new AgentProviderRegistry([provider], NullLogger<AgentProviderRegistry>.Instance),
                git ?? new GitWorktreeService(NullLogger<GitWorktreeService>.Instance),
                new SharedDriveContext(NullLogger<SharedDriveContext>.Instance),
                new PlanParserService(NullLogger<PlanParserService>.Instance),
                Hub,
                Telemetry,
                NullLogger<AgentOrchestrationService>.Instance);

        public async Task<AgentTeam> PlaybookAsync()
        {
            await using var ctx = Db.CreateDbContext();
            return await ctx.AgentTeams.Include(t => t.Members).SingleAsync();
        }

        public async Task<AgentSession> SessionAsync(
            string? providerId = "fake", string? plan = PlanJson, WorkspaceMode mode = WorkspaceMode.CurrentBranch) => new()
        {
            Team = await PlaybookAsync(),
            TaskDescription = "do the thing",
            RepoPath = Repo.Path,
            WorkspaceMode = mode,
            ProviderId = providerId,
            ModelId = "fake-model-1",
            Drive = new Drive { HuddlePlan = plan },
        };

        /// <summary>Polls until the drive leaves InProgress and its outcome has been reported.</summary>
        public async Task<Drive> WaitForOutcomeAsync(string driveId)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                await using var ctx = Db.CreateDbContext();
                var drive = await ctx.Drives
                    .Include(d => d.Plays).Include(d => d.Logs).Include(d => d.Turns)
                    .SingleAsync(d => d.DriveId == driveId);
                bool reported;
                lock (Telemetry.Outcomes) reported = Telemetry.Outcomes.Any(o => o.DriveId == driveId);
                if (drive.Status != DriveStatus.InProgress && reported) return drive;
                await Task.Delay(50);
            }
            throw new TimeoutException($"Drive {driveId} did not reach a reported terminal state.");
        }

        public DriveResult OutcomeOf(string driveId)
        {
            lock (Telemetry.Outcomes) return Telemetry.Outcomes.Single(o => o.DriveId == driveId);
        }

        public string? CompletionStatusSentTo(string driveId) =>
            Hub.PayloadsFor(driveId, "DriveCompleted")
                .Select(p => RecordingHubContext.Prop(p, "Status") as string)
                .LastOrDefault();

        public void Dispose()
        {
            Db.Dispose();
            Repo.Dispose();
        }
    }

    [Fact]
    public async Task An_unknown_provider_id_is_a_turnover_for_provider_unavailable()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider("unused"));

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync(providerId: "not-installed"));
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.NotNull(drive.CompletedAt);
        Assert.Equal(TurnoverReason.ProviderUnavailable, h.OutcomeOf(drive.DriveId).TurnoverReason);
        Assert.Equal("Turnover", h.CompletionStatusSentTo(drive.DriveId));
        Assert.Contains(drive.Logs, l => l.Message.StartsWith("TURNOVER!") && l.Message.Contains("not-installed"));
    }

    [Fact]
    public async Task No_available_provider_is_a_turnover_for_provider_unavailable()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider("unused") { IsAvailable = false });

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync(providerId: null));
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.Equal(TurnoverReason.ProviderUnavailable, h.OutcomeOf(drive.DriveId).TurnoverReason);
        Assert.Contains(drive.Logs, l => l.Message.Contains("No agent providers are available"));
    }

    [Fact]
    public async Task A_workspace_that_cannot_be_prepared_is_a_turnover_before_any_agent_runs()
    {
        using var h = new Harness();
        var provider = new FakeAgentProvider("unused");
        var orchestrator = h.Orchestrator(provider, new ThrowingGitWorktreeService());

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync(mode: WorkspaceMode.PrWorktree));
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.Equal(TurnoverReason.WorkspaceSetupFailed, h.OutcomeOf(drive.DriveId).TurnoverReason);
        Assert.Empty(provider.Calls);
        Assert.Empty(drive.Plays);
        Assert.Equal("Turnover", h.CompletionStatusSentTo(drive.DriveId));
    }

    [Fact]
    public async Task A_plan_the_quarterback_cannot_produce_is_a_turnover_for_planning_failed()
    {
        using var h = new Harness();
        // A prose huddle forces a re-prompt; a provider with no scripted answer throws there.
        var orchestrator = h.Orchestrator(new FakeAgentProvider());

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync(plan: "we talked about it, no json here"));
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.Equal(TurnoverReason.PlanningFailed, h.OutcomeOf(drive.DriveId).TurnoverReason);
        Assert.Empty(drive.Plays);
    }

    [Fact]
    public async Task An_agent_that_reports_an_error_fails_its_play_and_turns_the_drive_over()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider { ErrorResult = "codex exited with code 2: rate limited" });

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync());
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        var failed = Assert.Single(drive.Plays, p => p.Status == PlayStatus.Failed);
        Assert.Contains("rate limited", failed.Output);
        Assert.NotNull(failed.CompletedAt);

        // The reply, error and all, is kept as a turn so the transcript shows what happened.
        Assert.Contains(drive.Turns, t => t.Role == "assistant" && t.Content.Contains("rate limited"));
        Assert.Contains(h.Hub.PayloadsFor(drive.DriveId, "AgentStatusUpdate"),
            p => (string?)RecordingHubContext.Prop(p, "Status") == "Failed");
        Assert.Equal("Turnover", h.CompletionStatusSentTo(drive.DriveId));

        var outcome = h.OutcomeOf(drive.DriveId);
        Assert.Equal(1, outcome.FailedPlayCount);
        Assert.Equal(TurnoverReason.PlayFailed, outcome.TurnoverReason);
    }

    [Fact]
    public async Task A_provider_that_crashes_mid_play_fails_the_play_and_turns_the_drive_over()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider("unused")
        {
            ThrowOnStream = new IOException("the CLI process could not be started"),
        });

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync());
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.Contains(drive.Plays, p => p.Status == PlayStatus.Failed);
        Assert.Contains(drive.Logs, l => l.Message.Contains("could not be started"));
        Assert.Equal(TurnoverReason.PlayFailed, h.OutcomeOf(drive.DriveId).TurnoverReason);
    }

    [Fact]
    public async Task Cancelling_a_running_drive_marks_it_cancelled_and_tells_the_client()
    {
        using var h = new Harness();
        var provider = new HangingAgentProvider();
        var orchestrator = h.Orchestrator(provider);

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync(providerId: "hanging"));
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await orchestrator.CancelDriveAsync(started.DriveId);
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Cancelled, drive.Status);
        Assert.NotNull(drive.CompletedAt);
        Assert.Equal("Cancelled", h.CompletionStatusSentTo(drive.DriveId));
        Assert.Equal(TurnoverReason.Cancelled, h.OutcomeOf(drive.DriveId).TurnoverReason);
        Assert.DoesNotContain(drive.Plays, p => p.Status == PlayStatus.Completed);
    }

    [Fact]
    public async Task Cancelling_a_drive_that_is_not_running_is_a_no_op()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider("unused"));

        await orchestrator.CancelDriveAsync("never-started");

        Assert.Empty(h.Hub.Sent);
    }

    [Fact]
    public async Task Snapping_from_the_huddle_promotes_the_draft_row_instead_of_inserting_a_second_drive()
    {
        using var h = new Harness();
        var orchestrator = h.Orchestrator(new FakeAgentProvider("built it"));
        var session = await h.SessionAsync();
        session.Name = "  Login drive  ";

        var draft = await new DrivesNewServiceDA(h.Db).CreateDraftDriveAsync(session);
        Assert.Equal(DriveStatus.Huddle, draft.Status);
        session.Drive = draft;
        session.Drive.HuddlePlan = PlanJson;
        session.TaskDescription = "the refined task";

        var started = await orchestrator.StartDriveAsync(session);

        Assert.Equal(draft.Id, started.Id);
        Assert.Equal(draft.DriveId, started.DriveId);
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        await using var ctx = h.Db.CreateDbContext();
        var row = await ctx.Drives.SingleAsync();
        Assert.Equal(draft.Id, row.Id);
        Assert.Equal(DriveStatus.Touchdown, drive.Status);
        Assert.Equal("Login drive", row.Name);
        Assert.Equal("the refined task", row.TaskDescription);
        Assert.Equal(PlanJson, row.HuddlePlan);
        Assert.Equal(h.Repo.Path, row.WorkspacePath);
    }

    [Fact]
    public async Task A_successful_drive_persists_every_play_and_reports_a_touchdown()
    {
        using var h = new Harness();
        var provider = new FakeAgentProvider("built it");
        var orchestrator = h.Orchestrator(provider);

        var started = await orchestrator.StartDriveAsync(await h.SessionAsync());
        var drive = await h.WaitForOutcomeAsync(started.DriveId);

        Assert.Equal(DriveStatus.Touchdown, drive.Status);
        Assert.Equal(2, drive.Plays.Count);
        Assert.All(drive.Plays, p => Assert.Equal(PlayStatus.Completed, p.Status));
        Assert.All(drive.Plays, p => Assert.Contains("built it", p.Output));
        Assert.Equal("Touchdown", h.CompletionStatusSentTo(drive.DriveId));

        var outcome = h.OutcomeOf(drive.DriveId);
        Assert.Equal(DriveStatus.Touchdown, outcome.Status);
        Assert.Null(outcome.TurnoverReason);
        Assert.Equal(PlanSource.HuddleJson, outcome.PlanSource);
        Assert.Equal(2, outcome.PlayCount);

        // The board was announced once, and each play went Running then Completed.
        Assert.Single(h.Hub.PayloadsFor(drive.DriveId, "PlaysReady"));
        var statuses = h.Hub.PayloadsFor(drive.DriveId, "AgentStatusUpdate")
            .Select(p => (string?)RecordingHubContext.Prop(p, "Status")).ToList();
        Assert.Equal(2, statuses.Count(s => s == "Running"));
        Assert.Equal(2, statuses.Count(s => s == "Completed"));
    }
}
