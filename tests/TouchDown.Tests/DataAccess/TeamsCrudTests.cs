using Microsoft.EntityFrameworkCore;
using TD.Areas.Teams.Index;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// Team and roster editing. The guards matter more than the happy paths: deleting a team
/// that drives reference would orphan their history, and a team with no leader cannot plan.
/// </summary>
public class TeamsCrudTests
{
    private static TeamsIndexServiceDA Da(TestDb db) => new(db);

    private static async Task<int> SeedTeamAsync(TestDb db, string name, bool withLeader = true)
    {
        await using var ctx = db.CreateDbContext();
        var team = new AgentTeam { Name = name, Description = "test team" };
        if (withLeader)
            team.Members.Add(new AgentMember { Name = "QB", Role = AgentRole.Leader, Model = ClaudeModel.Opus });
        ctx.AgentTeams.Add(team);
        await ctx.SaveChangesAsync();
        return team.Id;
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_PersistsIt()
    {
        using var db = TestDb.Create();

        var team = await Da(db).CreateTeamAsync("Special Squad", "for special work");

        await using var ctx = db.CreateDbContext();
        var saved = await ctx.AgentTeams.FindAsync(team.Id);
        Assert.Equal("Special Squad", saved!.Name);
        Assert.Equal("for special work", saved.Description);
        Assert.False(saved.IsDefault);
    }

    [Fact]
    public async Task RenameTeam_UpdatesNameAndDescription()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Old Name");

        await Da(db).RenameTeamAsync(id, "New Name", "new description");

        await using var ctx = db.CreateDbContext();
        var team = await ctx.AgentTeams.FindAsync(id);
        Assert.Equal("New Name", team!.Name);
        Assert.Equal("new description", team.Description);
    }

    [Fact]
    public async Task DeleteTeam_RemovesItAndItsMembers()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Disposable");

        await Da(db).DeleteTeamAsync(id);

        await using var ctx = db.CreateDbContext();
        Assert.Null(await ctx.AgentTeams.FindAsync(id));
        Assert.Empty(await ctx.AgentMembers.Where(m => m.AgentTeamId == id).ToListAsync());
    }

    [Fact]
    public async Task DeleteTeam_IsRefusedWhenDrivesReferenceIt()
    {
        // Drive.AgentTeamId is required, so deleting the team would orphan the drive and
        // break the monitor page for it.
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "In Use");
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(new Drive { TaskDescription = "some work", AgentTeamId = id });
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<TeamsIndexServiceDAException>(() => Da(db).DeleteTeamAsync(id));

        Assert.Contains("cannot be deleted", ex.Message);
        await using var verify = db.CreateDbContext();
        Assert.NotNull(await verify.AgentTeams.FindAsync(id));
    }

    [Fact]
    public async Task DeleteTeam_IsRefusedForTheDefaultTeam()
    {
        // Leaving no default would break the New Drive wizard's initial selection.
        using var db = TestDb.Create();

        var ex = await Assert.ThrowsAsync<TeamsIndexServiceDAException>(
            () => Da(db).DeleteTeamAsync(PlaybookSeed.TeamId));

        Assert.Contains("default team", ex.Message);
    }

    [Fact]
    public async Task SetDefaultTeam_LeavesExactlyOneDefault()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Challenger");

        await Da(db).SetDefaultTeamAsync(id);

        await using var ctx = db.CreateDbContext();
        var defaults = await ctx.AgentTeams.Where(t => t.IsDefault).ToListAsync();
        Assert.Single(defaults);
        Assert.Equal(id, defaults[0].Id);
    }

    [Fact]
    public async Task SetDefaultTeam_ThenTheOldDefaultBecomesDeletable()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Challenger");
        await Da(db).SetDefaultTeamAsync(id);

        // The seeded Playbook has no drives here, so it can now go.
        await Da(db).DeleteTeamAsync(PlaybookSeed.TeamId);

        await using var ctx = db.CreateDbContext();
        Assert.Null(await ctx.AgentTeams.FindAsync(PlaybookSeed.TeamId));
    }

    [Fact]
    public async Task DeletingAnUnknownTeam_Throws()
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<TeamsIndexServiceDAException>(() => Da(db).DeleteTeamAsync(999_999));
    }

    // ── Members ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddMember_AttachesToTheTeam()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Growing");

        var member = await Da(db).AddMemberAsync(id, new AgentMember
        {
            Name = "The Punter",
            Role = AgentRole.Worker,
            Model = ClaudeModel.Haiku,
            Effort = AgentEffort.Low,
            MaxInstances = 3,
            SystemPrompt = "You punt.",
        });

        await using var ctx = db.CreateDbContext();
        var saved = await ctx.AgentMembers.FindAsync(member.Id);
        Assert.Equal("The Punter", saved!.Name);
        Assert.Equal(id, saved.AgentTeamId);
        Assert.Equal(3, saved.MaxInstances);
        Assert.Equal(AgentEffort.Low, saved.Effort);
    }

    [Fact]
    public async Task AddMember_ToAnUnknownTeam_Throws()
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<TeamsIndexServiceDAException>(
            () => Da(db).AddMemberAsync(999_999, new AgentMember { Name = "Nobody" }));
    }

    [Fact]
    public async Task RemoveMember_RemovesANonLeader()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Roster");
        var member = await Da(db).AddMemberAsync(id, new AgentMember { Name = "Worker", Role = AgentRole.Worker });

        await Da(db).RemoveMemberAsync(member.Id);

        await using var ctx = db.CreateDbContext();
        Assert.Null(await ctx.AgentMembers.FindAsync(member.Id));
    }

    [Fact]
    public async Task RemoveMember_IsRefusedForTheLastLeader()
    {
        // The Quarterback runs the huddle and produces the plan; a leaderless team cannot drive.
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "One Leader");
        await using var ctx = db.CreateDbContext();
        var leader = await ctx.AgentMembers.SingleAsync(m => m.AgentTeamId == id && m.Role == AgentRole.Leader);

        var ex = await Assert.ThrowsAsync<TeamsIndexServiceDAException>(() => Da(db).RemoveMemberAsync(leader.Id));

        Assert.Contains("needs a leader", ex.Message);
    }

    [Fact]
    public async Task RemoveMember_AllowsRemovingALeaderWhenAnotherRemains()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Two Leaders");
        var second = await Da(db).AddMemberAsync(id, new AgentMember { Name = "QB2", Role = AgentRole.Leader });

        await Da(db).RemoveMemberAsync(second.Id);

        await using var ctx = db.CreateDbContext();
        Assert.Single(await ctx.AgentMembers.Where(m => m.AgentTeamId == id && m.Role == AgentRole.Leader).ToListAsync());
    }

    [Fact]
    public async Task RenameMember_PersistsTheName()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Roster");
        var member = await Da(db).AddMemberAsync(id, new AgentMember { Name = "Before", Role = AgentRole.Worker });

        await Da(db).RenameMemberAsync(member.Id, "After");

        await using var ctx = db.CreateDbContext();
        Assert.Equal("After", (await ctx.AgentMembers.FindAsync(member.Id))!.Name);
    }

    [Fact]
    public async Task UpdateMemberMaxInstances_PersistsFanOut()
    {
        using var db = TestDb.Create();
        var id = await SeedTeamAsync(db, "Roster");
        var member = await Da(db).AddMemberAsync(id, new AgentMember { Name = "Line", Role = AgentRole.Worker });

        await Da(db).UpdateMemberMaxInstancesAsync(member.Id, 6);

        await using var ctx = db.CreateDbContext();
        Assert.Equal(6, (await ctx.AgentMembers.FindAsync(member.Id))!.MaxInstances);
    }
}

/// <summary>Validation that belongs to the service layer rather than the database.</summary>
public class TeamsServiceValidationTests
{
    private static TeamsIndexService Service(TestDb db) => new(new TeamsIndexServiceDA(db));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatingATeamWithoutAName_IsRejected(string name)
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<TeamsIndexServiceException>(() => Service(db).CreateTeamAsync(name, null));
    }

    [Fact]
    public async Task TeamNameAndDescriptionAreTrimmed()
    {
        using var db = TestDb.Create();

        var team = await Service(db).CreateTeamAsync("  Spaced  ", "  padded  ");

        Assert.Equal("Spaced", team.Name);
        Assert.Equal("padded", team.Description);
    }

    [Fact]
    public async Task AddingAMemberWithoutANameIsRejected()
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<TeamsIndexServiceException>(
            () => Service(db).AddMemberAsync(PlaybookSeed.TeamId, new AgentMember { Name = "  " }));
    }

    [Fact]
    public async Task FanOutBelowOneIsRejected()
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<TeamsIndexServiceException>(
            () => Service(db).UpdateMemberMaxInstancesAsync(1, 0));
    }

    [Fact]
    public async Task AddedMemberFanOutIsNormalisedToAtLeastOne()
    {
        using var db = TestDb.Create();

        var member = await Service(db).AddMemberAsync(
            PlaybookSeed.TeamId, new AgentMember { Name = "Zeroed", MaxInstances = 0 });

        Assert.Equal(1, member.MaxInstances);
    }

    [Fact]
    public async Task DeleteRefusalMessageReachesTheCaller()
    {
        // The guard text tells the user what to do, so it must survive the service layer
        // rather than being replaced with a generic failure.
        using var db = TestDb.Create();

        var ex = await Assert.ThrowsAsync<TeamsIndexServiceException>(
            () => Service(db).DeleteTeamAsync(PlaybookSeed.TeamId));

        Assert.Contains("default team", ex.Message);
    }
}
