using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TD.Data;

namespace TouchDown.Tests.TestSupport;

/// <summary>
/// An isolated in-memory SQLite database with the real schema applied, plus an
/// <see cref="IDbContextFactory{TContext}"/> matching how the app resolves contexts.
///
/// SQLite (rather than the EF in-memory provider) so relational behaviour — cascade
/// deletes, unique indexes, column types — is exercised as it is in production.
/// The connection is held open for the fixture's lifetime because an in-memory
/// database is dropped when its last connection closes.
/// </summary>
public sealed class TestDb : IDisposable, IDbContextFactory<TDDbContext>
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TDDbContext> _options;

    private TestDb(SqliteConnection connection, DbContextOptions<TDDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    /// <param name="seed">
    /// When true the context's seed data (the default "The Playbook" team) is applied,
    /// matching a freshly-migrated install.
    /// </param>
    public static TestDb Create(bool seed = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TDDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new TestDb(connection, options);

        using var ctx = db.CreateDbContext();
        // EnsureCreated applies the model (including HasData seeding) without needing
        // the migration history; the migrations themselves are verified separately.
        ctx.Database.EnsureCreated();

        if (!seed)
        {
            ctx.AgentTeams.RemoveRange(ctx.AgentTeams);
            ctx.SaveChanges();
        }

        return db;
    }

    public TDDbContext CreateDbContext() => new(_options);

    public Task<TDDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        _connection.Dispose();
    }
}
