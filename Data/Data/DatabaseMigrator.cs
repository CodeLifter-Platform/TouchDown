using Microsoft.EntityFrameworkCore;

namespace TD.Data;

/// <summary>
/// Brings a TouchDown database up to the current schema at startup.
///
/// Early installs created their database with <c>EnsureCreated</c>, which lays down the
/// tables but records no migration history. Running the migrations against such a database
/// would fail on <c>InitialCreate</c> (the tables already exist). So when the tables are
/// there and the history is not, <c>InitialCreate</c> is recorded as applied first and only
/// the newer migrations run. A fresh database and an already-migrated one both take the
/// plain migration path.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>The first migration, which a legacy EnsureCreated database already embodies.</summary>
    public const string InitialCreateMigrationId = "20260323205938_InitialCreate";

    public static async Task MigrateAsync(TDDbContext db, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AgentTeams';";
            var tablesExist = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) > 0;

            if (tablesExist)
            {
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """;
                await cmd.ExecuteNonQueryAsync(ct);

                cmd.CommandText = $"""
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('{InitialCreateMigrationId}', '10.0.5');
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        finally
        {
            // An in-memory SQLite database lives only as long as its connection, so a caller
            // that handed us an open connection keeps it.
            if (!wasOpen) await conn.CloseAsync();
        }

        await db.Database.MigrateAsync(ct);
    }
}
