using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TD.Data;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// Production applies migrations at startup, so the chain has to apply cleanly from
/// empty. Other tests use EnsureCreated for speed; these exercise the real path.
/// </summary>
public class MigrationTests
{
    private static DbContextOptions<TDDbContext> OptionsFor(SqliteConnection connection) =>
        new DbContextOptionsBuilder<TDDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task AllMigrations_ApplyToAnEmptyDatabase()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = new TDDbContext(OptionsFor(connection));

        await ctx.Database.MigrateAsync();

        var applied = await ctx.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task MigratedDatabase_ContainsTheSeededPlaybook()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = new TDDbContext(OptionsFor(connection));
        await ctx.Database.MigrateAsync();

        var team = await ctx.AgentTeams.Include(t => t.Members).SingleAsync();

        Assert.Equal("The Playbook", team.Name);
        Assert.True(team.IsDefault);
        Assert.Equal(6, team.Members.Count);
        Assert.Contains(team.Members, m => m.Role == AgentRole.Leader);
        Assert.Contains(team.Members, m => m.Role == AgentRole.Researcher);
        Assert.Contains(team.Members, m => m.Name == "The Defensive Line");
    }

    [Fact]
    public void The_model_has_no_changes_the_migrations_do_not_know_about()
    {
        // A model edit without `dotnet ef migrations add` would ship a schema the migrations
        // never create. GetPendingMigrations cannot see this (it compares the history table to
        // the migration list, not the model), so ask EF to diff the model against the snapshot.
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var ctx = new TDDbContext(OptionsFor(connection));

        Assert.False(ctx.Database.HasPendingModelChanges(),
            "The model differs from the last migration snapshot; add a migration.");
    }

    [Fact]
    public async Task DriveIdIsUnique()
    {
        using var db = TestDb.Create();
        await using var ctx = db.CreateDbContext();
        ctx.Drives.Add(new Drive { DriveId = "duplicate123", TaskDescription = "a", AgentTeamId = 1 });
        await ctx.SaveChangesAsync();

        ctx.Drives.Add(new Drive { DriveId = "duplicate123", TaskDescription = "b", AgentTeamId = 1 });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingADrive_CascadesToItsChildren()
    {
        using var db = TestDb.Create();
        await using var ctx = db.CreateDbContext();
        var drive = new Drive { TaskDescription = "a", AgentTeamId = 1 };
        drive.Plays.Add(new Play { Description = "p" });
        drive.Logs.Add(new DriveLog { AgentName = "System", Message = "m" });
        drive.Turns.Add(new DriveTurn { Phase = TurnPhase.Huddle, Role = "user", AgentName = "Coach", Content = "c" });
        ctx.Drives.Add(drive);
        await ctx.SaveChangesAsync();

        ctx.Drives.Remove(drive);
        await ctx.SaveChangesAsync();

        Assert.Empty(ctx.Plays);
        Assert.Empty(ctx.DriveLogs);
        Assert.Empty(ctx.DriveTurns);
    }

    [Fact]
    public async Task LongAgentOutputIsPersistedIntact()
    {
        // Play.Output allows 50k and DriveTurn.Content 200k; agent transcripts hit these.
        using var db = TestDb.Create();
        var output = new string('x', 50_000);
        var content = new string('y', 200_000);

        await using (var ctx = db.CreateDbContext())
        {
            var drive = new Drive { TaskDescription = "a", AgentTeamId = 1 };
            drive.Plays.Add(new Play { Description = "p", Output = output });
            drive.Turns.Add(new DriveTurn
            {
                Phase = TurnPhase.Execution, Role = "assistant", AgentName = "The Offensive Line", Content = content
            });
            ctx.Drives.Add(drive);
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateDbContext();
        Assert.Equal(50_000, (await verify.Plays.SingleAsync()).Output!.Length);
        Assert.Equal(200_000, (await verify.DriveTurns.SingleAsync()).Content.Length);
    }
}
