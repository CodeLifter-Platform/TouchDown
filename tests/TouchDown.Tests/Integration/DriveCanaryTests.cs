using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TD.Areas.Drives.New;
using TD.Data;
using TD.Models;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Integration;

/// <summary>
/// The integration canary: the exact wiring the product ships, in-process. A drive is created
/// from the New Drive area's service, promoted to InProgress by the orchestrator, planned by
/// the real plan parser from a huddle that already holds JSON, executed by a fake provider
/// swapped in through DI, written to the real SQLite schema, and reported to a real SignalR
/// client over the hosted hub. Only the model CLI is faked.
/// </summary>
[Collection(HostedAppCollection.Name)]
public class DriveCanaryTests
{
    private static readonly TimeSpan DriveTimeout = TimeSpan.FromSeconds(60);

    private const string PlanJson = """
        {"summary":"Ship the login page","assignments":[
          {"agent_role":"Worker","agent_name":"The Offensive Line","task":"build the login page","depends_on":[],"files":["Login.razor"],"priority":1},
          {"agent_role":"Validator","agent_name":"The Safety","task":"review the login page","depends_on":[0],"files":["Login.razor"],"priority":2}
        ]}
        """;

    [Fact]
    public async Task A_drive_runs_from_huddle_to_touchdown_through_the_shipped_wiring()
    {
        var provider = new FakeAgentProvider("Implemented the login page.", providerId: "fake");
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        using var app = new HostedApp(testServices: services =>
        {
            services.RemoveAll<IAgentProvider>();
            services.AddSingleton<IAgentProvider>(provider);
        });

        using var scope = app.Services.CreateScope();
        var newDrive = scope.ServiceProvider.GetRequiredService<IDrivesNewService>();
        var team = (await newDrive.GetAvailableTeamsAsync()).Single(t => t.IsDefault);

        var session = new AgentSession
        {
            Team = team,
            Name = "Login drive",
            TaskDescription = "Add a login page",
            RepoPath = repo.Path,
            SourceType = SourceType.GitRepo,
            WorkspaceMode = WorkspaceMode.CurrentBranch,
            ProviderId = "fake",
            ModelId = "fake-model-1",
            MaxParallelism = 2,
        };

        // The huddle opens: a draft drive is written so the conversation persists as it happens.
        session.Drive = await newDrive.CreateDraftDriveAsync(session);
        await newDrive.AddTurnAsync(new DriveTurn
        {
            DriveId = session.Drive.Id, Phase = TurnPhase.Huddle, Role = "user", AgentName = "Head Coach", Content = "Add a login page"
        });
        session.Drive.HuddlePlan = PlanJson;

        // The monitor page connects before the snap, exactly as the UI does.
        await using var client = await app.ConnectToDriveAsync(session.Drive.DriveId);
        var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playsReady = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = new ConcurrentBag<(string Agent, string Status)>();
        using var onCompleted = client.On<JsonElement>("DriveCompleted",
            el => completed.TrySetResult(el.GetProperty("Status").GetString() ?? ""));
        using var onPlays = client.On<JsonElement>("PlaysReady",
            el => playsReady.TrySetResult(el.GetProperty("Plays").GetArrayLength()));
        using var onStatus = client.On<JsonElement>("AgentStatusUpdate",
            el => statuses.Add((el.GetProperty("AgentName").GetString() ?? "", el.GetProperty("Status").GetString() ?? "")));

        var started = await newDrive.StartDriveAsync(session);

        // The draft row was promoted, not duplicated.
        Assert.Equal(session.Drive.Id, started.Id);
        Assert.Equal(DriveStatus.InProgress, started.Status);

        var outcome = await completed.Task.WaitAsync(DriveTimeout);
        if (outcome != "Touchdown")
            Assert.Fail($"Expected a Touchdown but the drive ended as {outcome}. Drive log:\n{await DriveLogAsync(scope)}");
        Assert.Equal(2, await playsReady.Task.WaitAsync(DriveTimeout));
        Assert.Contains(statuses, s => s.Agent.StartsWith("The Offensive Line") && s.Status == "Completed");
        Assert.Contains(statuses, s => s.Agent == "The Safety" && s.Status == "Completed");

        // Persisted state is what the monitor page reloads from.
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var drive = await db.Drives
            .Include(d => d.Plays).Include(d => d.Turns).Include(d => d.Logs)
            .SingleAsync();

        Assert.Equal(DriveStatus.Touchdown, drive.Status);
        Assert.NotNull(drive.CompletedAt);
        Assert.Equal("Login drive", drive.Name);
        Assert.Equal("fake", drive.ProviderId);
        Assert.Equal(repo.Path, drive.WorkspacePath);

        Assert.Equal(2, drive.Plays.Count);
        Assert.All(drive.Plays, p =>
        {
            Assert.Equal(PlayStatus.Completed, p.Status);
            Assert.Contains("Implemented the login page", p.Output);
            Assert.NotNull(p.StartedAt);
            Assert.NotNull(p.CompletedAt);
        });

        // Huddle opener, the Quarterback's plan comment, then a prompt and a reply per play.
        Assert.Single(drive.Turns, t => t.Phase == TurnPhase.Huddle && t.Role == "user");
        Assert.Single(drive.Turns, t => t.Phase == TurnPhase.Planning && t.Role == "comment");
        Assert.Equal(2, drive.Turns.Count(t => t.Phase == TurnPhase.Execution && t.Role == "user"));
        Assert.Equal(2, drive.Turns.Count(t => t.Phase == TurnPhase.Execution && t.Role == "assistant"));
        Assert.Contains(drive.Logs, l => l.Message.Contains("TOUCHDOWN"));

        // The validator ran after the worker and was handed the worker's output through the
        // shared drive context on disk.
        Assert.Equal(2, provider.Calls.Count);
        Assert.Contains("Implemented the login page", provider.Calls[1].Prompt);
        Assert.True(Directory.Exists(Path.Combine(repo.Path, ".touchdown", "agent-outputs")));
    }

    /// <summary>The persisted drive log, for a failure message that says what went wrong.</summary>
    private static async Task<string> DriveLogAsync(IServiceScope scope)
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var lines = await db.DriveLogs.OrderBy(l => l.Id).Select(l => $"[{l.AgentName}] {l.Message}").ToListAsync();
        return string.Join("\n", lines);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/teams")]
    [InlineData("/new-drive")]
    [InlineData("/settings")]
    public async Task Every_page_renders_in_production(string path)
    {
        using var app = new HostedApp();
        using var client = app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
