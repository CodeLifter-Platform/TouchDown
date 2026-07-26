using Microsoft.EntityFrameworkCore;
using TD.Areas.Drives.Monitor;
using TD.Areas.Home.Index;
using TD.Areas.Teams.Index;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// The DA layer is the only code that touches the database, so these run against a real
/// SQLite schema rather than a mocked context.
/// </summary>
public class HomeIndexServiceDATests
{
    private static Drive NewDrive(string task, DateTime createdAt) => new()
    {
        TaskDescription = task,
        AgentTeamId = 1,
        CreatedAt = createdAt,
    };

    [Fact]
    public async Task GetRecentDrivesAsync_ReturnsNewestFirst()
    {
        using var db = TestDb.Create();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.AddRange(
                NewDrive("oldest", baseTime),
                NewDrive("newest", baseTime.AddHours(2)),
                NewDrive("middle", baseTime.AddHours(1)));
            await ctx.SaveChangesAsync();
        }

        var drives = await new HomeIndexServiceDA(db).GetRecentDrivesAsync();

        Assert.Equal(["newest", "middle", "oldest"], drives.Select(d => d.TaskDescription));
    }

    [Fact]
    public async Task GetRecentDrivesAsync_RespectsTheCount()
    {
        using var db = TestDb.Create();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var ctx = db.CreateDbContext())
        {
            for (var i = 0; i < 10; i++)
                ctx.Drives.Add(NewDrive($"task {i}", baseTime.AddMinutes(i)));
            await ctx.SaveChangesAsync();
        }

        var drives = await new HomeIndexServiceDA(db).GetRecentDrivesAsync(3);

        Assert.Equal(3, drives.Count);
        Assert.Equal("task 9", drives[0].TaskDescription);
    }

    [Fact]
    public async Task GetRecentDrivesAsync_IncludesTheTeamForDisplay()
    {
        // The dashboard renders the team name; without the Include it would be null.
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(NewDrive("task", DateTime.UtcNow));
            await ctx.SaveChangesAsync();
        }

        var drive = Assert.Single(await new HomeIndexServiceDA(db).GetRecentDrivesAsync());

        Assert.NotNull(drive.AgentTeam);
        Assert.Equal("The Playbook", drive.AgentTeam!.Name);
    }

    [Fact]
    public async Task GetRecentDrivesAsync_EmptyDatabase_ReturnsEmpty()
    {
        using var db = TestDb.Create();

        Assert.Empty(await new HomeIndexServiceDA(db).GetRecentDrivesAsync());
    }
}

public class TeamsIndexServiceDATests
{
    [Fact]
    public async Task GetAllTeamsAsync_IncludesMembersAndRules()
    {
        using var db = TestDb.Create();

        var teams = await new TeamsIndexServiceDA(db).GetAllTeamsAsync();

        var team = Assert.Single(teams);
        Assert.Equal("The Playbook", team.Name);
        Assert.Equal(6, team.Members.Count);
        Assert.NotEmpty(team.CommunicationRules);
    }

    [Fact]
    public async Task UpdateMemberPromptAsync_PersistsTheChange()
    {
        using var db = TestDb.Create();
        var da = new TeamsIndexServiceDA(db);
        var memberId = (await da.GetAllTeamsAsync())[0].Members[0].Id;

        await da.UpdateMemberPromptAsync(memberId, "You are now a punter.");

        await using var ctx = db.CreateDbContext();
        Assert.Equal("You are now a punter.", (await ctx.AgentMembers.FindAsync(memberId))!.SystemPrompt);
    }

    [Fact]
    public async Task UpdateMemberEffortAsync_PersistsTheChange()
    {
        using var db = TestDb.Create();
        var da = new TeamsIndexServiceDA(db);
        var memberId = (await da.GetAllTeamsAsync())[0].Members[0].Id;

        await da.UpdateMemberEffortAsync(memberId, AgentEffort.Max);

        await using var ctx = db.CreateDbContext();
        Assert.Equal(AgentEffort.Max, (await ctx.AgentMembers.FindAsync(memberId))!.Effort);
    }

    [Fact]
    public async Task UpdateMemberModelAsync_PersistsTheChange()
    {
        using var db = TestDb.Create();
        var da = new TeamsIndexServiceDA(db);
        var memberId = (await da.GetAllTeamsAsync())[0].Members[0].Id;

        await da.UpdateMemberModelAsync(memberId, ClaudeModel.Haiku);

        await using var ctx = db.CreateDbContext();
        Assert.Equal(ClaudeModel.Haiku, (await ctx.AgentMembers.FindAsync(memberId))!.Model);
    }

    [Fact]
    public async Task UpdatingAnUnknownMember_ThrowsADomainException()
    {
        using var db = TestDb.Create();
        var da = new TeamsIndexServiceDA(db);

        await Assert.ThrowsAsync<TeamsIndexServiceDAException>(
            () => da.UpdateMemberPromptAsync(999_999, "nobody"));
    }
}

public class DrivesMonitorServiceDATests
{
    private static async Task<string> SeedDriveAsync(TestDb db)
    {
        await using var ctx = db.CreateDbContext();
        var team = await ctx.AgentTeams.Include(t => t.Members).FirstAsync();
        var drive = new Drive { TaskDescription = "add login", AgentTeamId = team.Id };
        drive.Plays.Add(new Play { Description = "build it", OrderIndex = 0, AssignedMemberId = team.Members[0].Id });
        drive.Logs.Add(new DriveLog { AgentName = "System", Message = "started" });
        drive.Turns.Add(new DriveTurn { Phase = TurnPhase.Huddle, Role = "user", AgentName = "Head Coach", Content = "go" });
        ctx.Drives.Add(drive);
        await ctx.SaveChangesAsync();
        return drive.DriveId;
    }

    [Fact]
    public async Task GetDriveAsync_LoadsTheWholeAggregate()
    {
        using var db = TestDb.Create();
        var driveId = await SeedDriveAsync(db);

        var drive = await new DrivesMonitorServiceDA(db).GetDriveAsync(driveId);

        Assert.NotNull(drive);
        Assert.NotNull(drive!.AgentTeam);
        Assert.NotEmpty(drive.AgentTeam!.Members);
        Assert.Single(drive.Plays);
        Assert.NotNull(drive.Plays[0].AssignedMember);
        Assert.Single(drive.Logs);
        Assert.Single(drive.Turns);
    }

    [Fact]
    public async Task GetDriveAsync_UnknownId_ReturnsNull()
    {
        using var db = TestDb.Create();

        Assert.Null(await new DrivesMonitorServiceDA(db).GetDriveAsync("does-not-exist"));
    }

    [Fact]
    public async Task RenameDriveAsync_SetsAndTrimsTheName()
    {
        using var db = TestDb.Create();
        var driveId = await SeedDriveAsync(db);
        var da = new DrivesMonitorServiceDA(db);

        await da.RenameDriveAsync(driveId, "  Login rework  ");

        Assert.Equal("Login rework", (await da.GetDriveAsync(driveId))!.Name);
    }

    [Fact]
    public async Task RenameDriveAsync_BlankName_ClearsIt()
    {
        // A cleared name falls the display back to the task description.
        using var db = TestDb.Create();
        var driveId = await SeedDriveAsync(db);
        var da = new DrivesMonitorServiceDA(db);
        await da.RenameDriveAsync(driveId, "Something");

        await da.RenameDriveAsync(driveId, "   ");

        var drive = await da.GetDriveAsync(driveId);
        Assert.Null(drive!.Name);
        Assert.Equal("add login", drive.DisplayName);
    }

    [Fact]
    public async Task RenameDriveAsync_UnknownId_IsANoOp()
    {
        using var db = TestDb.Create();

        await new DrivesMonitorServiceDA(db).RenameDriveAsync("nope", "x");
    }
}
