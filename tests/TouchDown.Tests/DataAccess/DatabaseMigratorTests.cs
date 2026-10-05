using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TD.Data;
using TD.Models;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// Startup upgrades whatever database it finds in place: a fresh file, one already at the
/// current schema, and the early installs that were created with EnsureCreated and so have
/// tables but no migration history. That last shape is the one that used to make startup
/// fail on InitialCreate, and the one nobody has lying around to test against by hand.
/// </summary>
public class DatabaseMigratorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public DatabaseMigratorTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private TDDbContext Context() =>
        new(new DbContextOptionsBuilder<TDDbContext>().UseSqlite(_connection).Options);

    private async Task ExecAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// Builds the schema an EnsureCreated install of the first release had: the InitialCreate
    /// tables, with the history table dropped because EnsureCreated never wrote one.
    /// </summary>
    private async Task CreateLegacyDatabaseAsync()
    {
        await using var ctx = Context();
        await ctx.GetService<IMigrator>().MigrateAsync(DatabaseMigrator.InitialCreateMigrationId);
        await ExecAsync("DROP TABLE \"__EFMigrationsHistory\";");
    }

    [Fact]
    public async Task A_fresh_database_gets_the_whole_chain_and_the_seeded_playbook()
    {
        await using var ctx = Context();

        await DatabaseMigrator.MigrateAsync(ctx);

        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
        Assert.Contains(await ctx.AgentTeams.ToListAsync(), t => t.Name == PlaybookSeed.TeamName);
    }

    [Fact]
    public async Task A_legacy_ensure_created_database_is_upgraded_in_place_with_its_data_kept()
    {
        await CreateLegacyDatabaseAsync();
        // A drive from that era, written with the columns that existed then.
        await ExecAsync("""
            INSERT INTO "Drives" ("DriveId","Status","CreatedAt","TaskDescription","MaxParallelism","SourceType","WorkspaceMode","AgentTeamId")
            VALUES ('legacy-drive','3','2026-03-24 10:00:00','an old task','2','0','0','1');
            """);
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='__EFMigrationsHistory';"));

        await using var ctx = Context();
        await DatabaseMigrator.MigrateAsync(ctx);

        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
        var applied = (await ctx.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Contains(DatabaseMigrator.InitialCreateMigrationId, applied);
        Assert.True(applied.Count > 1, "the newer migrations should have run on top of the legacy schema");

        var drive = await ctx.Drives.SingleAsync(d => d.DriveId == "legacy-drive");
        Assert.Equal("an old task", drive.TaskDescription);
        Assert.Equal(DriveStatus.Touchdown, drive.Status);
        // A column added by a later migration reads as its default rather than failing.
        Assert.Null(drive.ProviderId);
        Assert.True(drive.OverrideTeamConfig);
    }

    [Fact]
    public async Task An_already_migrated_database_is_left_alone()
    {
        await using (var ctx = Context())
            await DatabaseMigrator.MigrateAsync(ctx);
        var historyRows = await ScalarAsync("SELECT COUNT(*) FROM \"__EFMigrationsHistory\";");

        await using (var ctx = Context())
            await DatabaseMigrator.MigrateAsync(ctx);

        Assert.Equal(historyRows, await ScalarAsync("SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        await using var verify = Context();
        Assert.Single(await verify.AgentTeams.ToListAsync());
    }
}
