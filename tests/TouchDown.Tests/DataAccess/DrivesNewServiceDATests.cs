using Microsoft.EntityFrameworkCore;
using TD.Areas.Drives.New;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// The New Drive area's data access: the draft that the Huddle writes before any model
/// call, the turns appended to it as the conversation happens, and custom teams.
/// </summary>
public class DrivesNewServiceDATests
{
    private static DrivesNewServiceDA Da(TestDb db) => new(db);

    private static AgentSession FullSession() => new()
    {
        Team = new AgentTeam { Id = PlaybookSeed.TeamId },
        Name = "  Login drive  ",
        TaskDescription = "Add a login page",
        MaxParallelism = 4,
        SourceType = SourceType.LocalFolder,
        RepoPath = "/home/dev/project",
        Branch = "main",
        WorkspaceMode = WorkspaceMode.FreshFolder,
        PrBranchName = "touchdown/login",
        ProviderId = "codex",
        ModelId = "gpt-5.4",
        Effort = AgentEffort.Medium,
        OverrideTeamConfig = false,
    };

    [Fact]
    public async Task The_draft_drive_carries_every_session_field_and_sits_in_the_huddle_state()
    {
        using var db = TestDb.Create();

        var draft = await Da(db).CreateDraftDriveAsync(FullSession());

        await using var verify = db.CreateDbContext();
        var row = await verify.Drives.SingleAsync();
        Assert.Equal(draft.Id, row.Id);
        Assert.Equal(DriveStatus.Huddle, row.Status);
        Assert.Equal("Login drive", row.Name);
        Assert.Equal("Add a login page", row.TaskDescription);
        Assert.Equal(4, row.MaxParallelism);
        Assert.Equal(SourceType.LocalFolder, row.SourceType);
        Assert.Equal("/home/dev/project", row.RepoPath);
        Assert.Equal("main", row.Branch);
        Assert.Equal(WorkspaceMode.FreshFolder, row.WorkspaceMode);
        Assert.Equal("touchdown/login", row.PrBranchName);
        Assert.Equal(PlaybookSeed.TeamId, row.AgentTeamId);
        Assert.Equal("codex", row.ProviderId);
        Assert.Equal("gpt-5.4", row.ModelId);
        Assert.Equal(AgentEffort.Medium, row.Effort);
        Assert.False(row.OverrideTeamConfig);
        Assert.Null(row.CompletedAt);
    }

    [Fact]
    public async Task A_blank_name_is_stored_as_no_name()
    {
        using var db = TestDb.Create();
        var session = FullSession();
        session.Name = "   ";

        await Da(db).CreateDraftDriveAsync(session);

        await using var verify = db.CreateDbContext();
        Assert.Null((await verify.Drives.SingleAsync()).Name);
    }

    [Fact]
    public async Task Turns_append_to_the_draft_in_order()
    {
        using var db = TestDb.Create();
        var da = Da(db);
        var draft = await da.CreateDraftDriveAsync(FullSession());
        var t0 = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        await da.AddTurnAsync(new DriveTurn { DriveId = draft.Id, Phase = TurnPhase.Huddle, Role = "user", AgentName = "Head Coach", Content = "first", Timestamp = t0 });
        await da.AddTurnAsync(new DriveTurn { DriveId = draft.Id, Phase = TurnPhase.Huddle, Role = "assistant", AgentName = "The Quarterback", Content = "second", Timestamp = t0.AddSeconds(5) });

        await using var verify = db.CreateDbContext();
        var turns = await verify.DriveTurns.OrderBy(t => t.Timestamp).ThenBy(t => t.Id).ToListAsync();
        Assert.Equal(["first", "second"], turns.Select(t => t.Content));
        Assert.All(turns, t => Assert.Equal(draft.Id, t.DriveId));
    }

    [Fact]
    public async Task A_turn_that_cannot_be_written_does_not_break_the_conversation()
    {
        // The DA promises that a failed turn write is logged and swallowed, because the live
        // huddle must keep going even if its transcript has a hole.
        using var db = TestDb.Create();

        await Da(db).AddTurnAsync(new DriveTurn { DriveId = 999_999, Role = "user", Content = "orphan" });

        await using var verify = db.CreateDbContext();
        Assert.Empty(verify.DriveTurns);
    }

    [Fact]
    public async Task A_custom_team_is_saved_with_its_members_and_is_not_the_default()
    {
        using var db = TestDb.Create();
        var team = new AgentTeam
        {
            Name = "Custom",
            Description = "mine",
            Members =
            [
                new AgentMember { Name = "QB", Role = AgentRole.Leader, Model = ClaudeModel.Opus },
                new AgentMember { Name = "Line", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet, MaxInstances = 3 },
            ],
        };

        var saved = await Da(db).SaveCustomTeamAsync(team);

        await using var verify = db.CreateDbContext();
        var row = await verify.AgentTeams.Include(t => t.Members).SingleAsync(t => t.Id == saved.Id);
        Assert.Equal("Custom", row.Name);
        Assert.False(row.IsDefault);
        Assert.Equal(2, row.Members.Count);
        Assert.Contains(row.Members, m => m.Name == "Line" && m.MaxInstances == 3);
    }

    [Fact]
    public async Task Available_teams_come_with_their_members_and_rules()
    {
        using var db = TestDb.Create();

        var teams = await Da(db).GetAvailableTeamsAsync();

        var playbook = Assert.Single(teams);
        Assert.Equal(PlaybookSeed.Members.Count, playbook.Members.Count);
        Assert.NotNull(playbook.GetLeader());
    }
}
