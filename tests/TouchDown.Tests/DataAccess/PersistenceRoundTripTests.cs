using Microsoft.EntityFrameworkCore;
using TD.Areas.Teams.Index;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// Every field of every entity survives a trip through SQLite, a second save of the same
/// row is an update rather than a duplicate, and a delete detaches or cascades as the model
/// promises. The schema is the real one; only the file is in memory.
/// </summary>
public class PersistenceRoundTripTests
{
    private static readonly DateTime Created = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

    private static Drive FullDrive() => new()
    {
        DriveId = "drive-abc123",
        Name = "Login drive",
        Status = DriveStatus.InProgress,
        CreatedAt = Created,
        CompletedAt = Created.AddMinutes(12),
        TaskDescription = "Add a login page",
        MaxParallelism = 3,
        SourceType = SourceType.LocalFolder,
        RepoPath = "/home/dev/project",
        Branch = "feature/login",
        WorkspaceMode = WorkspaceMode.PrWorktree,
        WorkspacePath = "/home/dev/project/.touchdown/worktrees/touchdown-login",
        PrBranchName = "touchdown/login",
        AgentTeamId = PlaybookSeed.TeamId,
        HuddlePlan = "{\"summary\":\"plan\"}",
        ProviderId = "codex",
        ModelId = "gpt-5.4",
        Effort = AgentEffort.Low,
        OverrideTeamConfig = false,
    };

    [Fact]
    public async Task A_drive_round_trips_every_field()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(FullDrive());
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        var d = await verify.Drives.SingleAsync();

        Assert.Equal("drive-abc123", d.DriveId);
        Assert.Equal("Login drive", d.Name);
        Assert.Equal(DriveStatus.InProgress, d.Status);
        Assert.Equal(Created, d.CreatedAt);
        Assert.Equal(Created.AddMinutes(12), d.CompletedAt);
        Assert.Equal("Add a login page", d.TaskDescription);
        Assert.Equal(3, d.MaxParallelism);
        Assert.Equal(SourceType.LocalFolder, d.SourceType);
        Assert.Equal("/home/dev/project", d.RepoPath);
        Assert.Equal("feature/login", d.Branch);
        Assert.Equal(WorkspaceMode.PrWorktree, d.WorkspaceMode);
        Assert.Equal("/home/dev/project/.touchdown/worktrees/touchdown-login", d.WorkspacePath);
        Assert.Equal("touchdown/login", d.PrBranchName);
        Assert.Equal(PlaybookSeed.TeamId, d.AgentTeamId);
        Assert.Equal("{\"summary\":\"plan\"}", d.HuddlePlan);
        Assert.Equal("codex", d.ProviderId);
        Assert.Equal("gpt-5.4", d.ModelId);
        Assert.Equal(AgentEffort.Low, d.Effort);
        Assert.False(d.OverrideTeamConfig);
    }

    [Fact]
    public async Task A_play_and_its_turns_and_logs_round_trip_every_field()
    {
        using var db = TestDb.Create();
        int memberId;
        await using (var ctx = db.CreateDbContext())
        {
            memberId = (await ctx.AgentMembers.FirstAsync(m => m.Role == AgentRole.Worker)).Id;
            var drive = FullDrive();
            drive.Plays.Add(new Play
            {
                Description = "build the login page",
                Status = PlayStatus.Completed,
                AssignedMemberId = memberId,
                StartedAt = Created.AddMinutes(1),
                CompletedAt = Created.AddMinutes(5),
                Output = "Implemented it.",
                OrderIndex = 4,
            });
            drive.Logs.Add(new DriveLog
            {
                Timestamp = Created.AddMinutes(2),
                Level = TD.Models.LogLevel.AgentOutput,
                AgentName = "The Offensive Line #1",
                Message = "Using tool: Edit",
            });
            ctx.Drives.Add(drive);
            await ctx.SaveChangesAsync();

            ctx.DriveTurns.Add(new DriveTurn
            {
                DriveId = drive.Id,
                PlayId = drive.Plays[0].Id,
                Phase = TurnPhase.Execution,
                Role = "assistant",
                AgentName = "The Offensive Line #1",
                Content = "Implemented it.",
                ToolsUsed = "[\"Read\",\"Edit\"]",
                CostUsd = 0.0421,
                Timestamp = Created.AddMinutes(5),
            });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        var play = await verify.Plays.Include(p => p.AssignedMember).SingleAsync();
        Assert.Equal("build the login page", play.Description);
        Assert.Equal(PlayStatus.Completed, play.Status);
        Assert.Equal(memberId, play.AssignedMemberId);
        Assert.NotNull(play.AssignedMember);
        Assert.Equal(Created.AddMinutes(1), play.StartedAt);
        Assert.Equal(Created.AddMinutes(5), play.CompletedAt);
        Assert.Equal("Implemented it.", play.Output);
        Assert.Equal(4, play.OrderIndex);

        var log = await verify.DriveLogs.SingleAsync();
        Assert.Equal(Created.AddMinutes(2), log.Timestamp);
        Assert.Equal(TD.Models.LogLevel.AgentOutput, log.Level);
        Assert.Equal("The Offensive Line #1", log.AgentName);
        Assert.Equal("Using tool: Edit", log.Message);

        var turn = await verify.DriveTurns.SingleAsync();
        Assert.Equal(play.Id, turn.PlayId);
        Assert.Equal(TurnPhase.Execution, turn.Phase);
        Assert.Equal("assistant", turn.Role);
        Assert.Equal("The Offensive Line #1", turn.AgentName);
        Assert.Equal("Implemented it.", turn.Content);
        Assert.Equal("[\"Read\",\"Edit\"]", turn.ToolsUsed);
        Assert.Equal(0.0421, turn.CostUsd);
        Assert.Equal(Created.AddMinutes(5), turn.Timestamp);
    }

    [Fact]
    public async Task A_team_with_members_and_rules_round_trips_every_field()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.AgentTeams.Add(new AgentTeam
            {
                Name = "Special Squad",
                Description = "for special work",
                IsDefault = false,
                Members =
                [
                    new AgentMember
                    {
                        Name = "Kicker", Role = AgentRole.DevOps, Model = ClaudeModel.Haiku,
                        Effort = AgentEffort.Medium, MaxInstances = 4, SystemPrompt = "You kick.",
                    }
                ],
                CommunicationRules = [new CommunicationRule { Style = CommStyle.Broadcast, Description = "short replies" }],
            });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        var team = await verify.AgentTeams
            .Include(t => t.Members).Include(t => t.CommunicationRules)
            .SingleAsync(t => t.Name == "Special Squad");
        Assert.Equal("for special work", team.Description);
        Assert.False(team.IsDefault);
        var member = Assert.Single(team.Members);
        Assert.Equal("Kicker", member.Name);
        Assert.Equal(AgentRole.DevOps, member.Role);
        Assert.Equal(ClaudeModel.Haiku, member.Model);
        Assert.Equal(AgentEffort.Medium, member.Effort);
        Assert.Equal(4, member.MaxInstances);
        Assert.Equal("You kick.", member.SystemPrompt);
        var rule = Assert.Single(team.CommunicationRules);
        Assert.Equal(CommStyle.Broadcast, rule.Style);
        Assert.Equal("short replies", rule.Description);
    }

    [Fact]
    public async Task Saving_a_loaded_drive_again_updates_the_row_rather_than_adding_one()
    {
        using var db = TestDb.Create();
        int id;
        await using (var ctx = db.CreateDbContext())
        {
            var drive = FullDrive();
            ctx.Drives.Add(drive);
            await ctx.SaveChangesAsync();
            id = drive.Id;
        }

        await using (var ctx = db.CreateDbContext())
        {
            var drive = await ctx.Drives.SingleAsync(d => d.Id == id);
            drive.Status = DriveStatus.Touchdown;
            drive.Name = "Renamed";
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        var saved = await verify.Drives.SingleAsync();
        Assert.Equal(id, saved.Id);
        Assert.Equal(DriveStatus.Touchdown, saved.Status);
        Assert.Equal("Renamed", saved.Name);
    }

    [Fact]
    public async Task Deleting_a_team_cascades_to_its_members_and_rules_but_not_to_other_teams()
    {
        using var db = TestDb.Create();
        int teamId;
        await using (var ctx = db.CreateDbContext())
        {
            var team = new AgentTeam
            {
                Name = "Doomed",
                Members = [new AgentMember { Name = "QB", Role = AgentRole.Leader }],
                CommunicationRules = [new CommunicationRule { Description = "y" }],
            };
            ctx.AgentTeams.Add(team);
            await ctx.SaveChangesAsync();
            teamId = team.Id;
        }

        await using (var ctx = db.CreateDbContext())
        {
            ctx.AgentTeams.Remove(await ctx.AgentTeams.SingleAsync(t => t.Id == teamId));
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        Assert.Empty(verify.AgentMembers.Where(m => m.AgentTeamId == teamId));
        Assert.Empty(verify.CommunicationRules.Where(r => r.AgentTeamId == teamId));
        Assert.Equal(PlaybookSeed.Members.Count, await verify.AgentMembers.CountAsync());
    }

    [Fact]
    public async Task Removing_a_member_whose_plays_are_on_record_is_refused_with_a_message_naming_the_member()
    {
        // Plays hold a nullable FK to the member that ran them. Deleting that member would
        // either orphan the history or, as the schema stands, fail at the database. Either
        // way the user needs to be told why, not shown a generic failure.
        using var db = TestDb.Create();
        int memberId;
        await using (var ctx = db.CreateDbContext())
        {
            var team = new AgentTeam
            {
                Name = "Veterans",
                Members =
                [
                    new AgentMember { Name = "QB", Role = AgentRole.Leader },
                    new AgentMember { Name = "Old Guard", Role = AgentRole.Worker },
                ],
            };
            ctx.AgentTeams.Add(team);
            await ctx.SaveChangesAsync();
            memberId = team.Members.Single(m => m.Name == "Old Guard").Id;

            var drive = new Drive { TaskDescription = "past work", AgentTeamId = team.Id };
            drive.Plays.Add(new Play { Description = "did it", AssignedMemberId = memberId, Status = PlayStatus.Completed });
            ctx.Drives.Add(drive);
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<TeamsIndexServiceDAException>(
            () => new TeamsIndexServiceDA(db).RemoveMemberAsync(memberId));

        Assert.Contains("Old Guard", ex.Message);
        await using var verify = db.CreateDbContext();
        Assert.NotNull(await verify.AgentMembers.FindAsync(memberId));
        Assert.Equal(memberId, (await verify.Plays.SingleAsync()).AssignedMemberId);
    }
}
