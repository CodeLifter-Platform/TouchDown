using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TD.Data;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.DataAccess;

/// <summary>
/// The built-in team was defined twice — as EF seed rows and as
/// <see cref="AgentTeam.CreateThePlaybook"/> — and had drifted apart. Both now project from
/// <see cref="PlaybookSeed"/>; these tests hold them together.
/// </summary>
public class PlaybookSeedTests
{
    private static DbContextOptions<TDDbContext> OptionsFor(SqliteConnection connection) =>
        new DbContextOptionsBuilder<TDDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task SeededTeamMatchesTheFactory()
    {
        // The exact drift this consolidation was for: the factory and the database
        // disagreeing about what the built-in team is.
        using var db = TestDb.Create();
        await using var ctx = db.CreateDbContext();
        var seeded = await ctx.AgentTeams.Include(t => t.Members).Include(t => t.CommunicationRules).SingleAsync();
        var factory = AgentTeam.CreateThePlaybook();

        Assert.Equal(factory.Name, seeded.Name);
        Assert.Equal(factory.Description, seeded.Description);
        Assert.Equal(factory.Members.Count, seeded.Members.Count);

        foreach (var expected in factory.Members)
        {
            var actual = seeded.Members.Single(m => m.Id == expected.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Role, actual.Role);
            Assert.Equal(expected.Model, actual.Model);
            Assert.Equal(expected.Effort, actual.Effort);
            Assert.Equal(expected.MaxInstances, actual.MaxInstances);
            Assert.Equal(expected.SystemPrompt, actual.SystemPrompt);
        }

        var expectedRule = factory.CommunicationRules.Single();
        var actualRule = seeded.CommunicationRules.Single();
        Assert.Equal(expectedRule.Style, actualRule.Style);
        Assert.Equal(expectedRule.Description, actualRule.Description);
    }

    [Fact]
    public void SeedMemberIdsAreStable()
    {
        // Installed databases carry these ids; renumbering would rewrite every row.
        Assert.Equal([1, 2, 4, 5, 6, 8], PlaybookSeed.Members.Select(m => m.Id).Order());
    }

    [Fact]
    public void SafetyPromptCoversBothLines()
    {
        // The stale seeded prompt predated the Defensive Line and named only the Offensive Line.
        var safety = PlaybookSeed.Members.Single(m => m.Name == "The Safety");

        Assert.Contains("Offensive Line", safety.SystemPrompt);
        Assert.Contains("Defensive Line", safety.SystemPrompt);
    }

    [Fact]
    public void EverySeededMemberHasAPrompt()
    {
        Assert.All(PlaybookSeed.Members, m => Assert.False(string.IsNullOrWhiteSpace(m.SystemPrompt)));
    }

    [Fact]
    public async Task MigrationRepairsAnUntouchedDefaultPrompt()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = new TDDbContext(OptionsFor(connection));
        await ctx.Database.MigrateAsync();

        var safety = await ctx.AgentMembers.SingleAsync(m => m.Id == 4);

        Assert.Equal(AgentDefaults.SafetySystemPrompt, safety.SystemPrompt);
    }

    [Fact]
    public async Task MigrationDoesNotClobberACustomisedPrompt()
    {
        // System prompts are editable from the Teams page. A seed repair must not silently
        // revert someone's edit, so the update is guarded on the old value still being present.
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using (var ctx = new TDDbContext(OptionsFor(connection)))
        {
            // Migrate to the state just before the consolidation, then customise the prompt.
            var migrator = ctx.GetService<IMigrator>();
            await migrator.MigrateAsync("20260613213540_AddDefensiveLine");

            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"AgentMembers\" SET \"SystemPrompt\" = 'My own house rules.' WHERE \"Id\" = 4;");
        }

        await using (var ctx = new TDDbContext(OptionsFor(connection)))
        {
            await ctx.Database.MigrateAsync();

            var safety = await ctx.AgentMembers.SingleAsync(m => m.Id == 4);
            Assert.Equal("My own house rules.", safety.SystemPrompt);
        }
    }
}
