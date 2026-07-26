using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TD.Data;
using TD.Services;

namespace TouchDown.Tests.Integration;

/// <summary>
/// Boots the real application the way `dotnet run` does.
///
/// This exists because the app used to crash on startup in Production: the recurring job
/// was registered through Hangfire's static API, which depends on JobStorage.Current being
/// initialized — and the only thing that initialized it was the Development-only Hangfire
/// dashboard. Every build passed and every unit test passed; nothing executed startup.
/// </summary>
public class StartupTests
{
    /// <summary>Boots the app against a throwaway SQLite file in the given environment.</summary>
    private sealed class TestApp : WebApplicationFactory<Program>
    {
        private readonly string _environment;
        private readonly string _dbPath;

        public TestApp(string environment)
        {
            _environment = environment;
            _dbPath = Path.Combine(Path.GetTempPath(), $"td-startup-{Guid.NewGuid():N}.db");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TouchDown"] = $"Data Source={_dbPath}"
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Application_StartsAndServesRequests(string environment)
    {
        // Regression: this threw "JobStorage instance has not been initialized" in
        // Production and the process died before binding a port.
        using var app = new TestApp(environment);

        using var client = app.CreateClient();
        var response = await client.GetAsync("/health");

        // Healthy or Unhealthy both prove startup completed — the CLIs may be absent here.
        Assert.Contains(response.StatusCode,
            new[] { System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.ServiceUnavailable });
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void RecurringJobRegistration_DoesNotThrow(string environment)
    {
        using var app = new TestApp(environment);

        // Forcing the host to build runs the whole startup path, including job registration.
        var jobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        Assert.NotNull(jobManager);
    }

    [Fact]
    public async Task Startup_AppliesMigrationsAndSeedsTheDefaultTeam()
    {
        using var app = new TestApp("Production");

        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(await db.AgentTeams.ToListAsync(), t => t.Name == "The Playbook");
    }

    [Fact]
    public void Startup_RegistersBothAgentProviders()
    {
        using var app = new TestApp("Production");

        var registry = app.Services.GetRequiredService<IAgentProviderRegistry>();

        Assert.Contains(registry.All, p => p.ProviderId == "claude-code");
        Assert.Contains(registry.All, p => p.ProviderId == "codex");
    }

    [Fact]
    public void Startup_RegistersTheOrphanedDriveReconciler()
    {
        using var app = new TestApp("Production");

        using var scope = app.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrphanedDriveReconciler>());
    }

    [Fact]
    public async Task ConnectionStringConfiguration_IsHonoured()
    {
        // The Dockerfile relies on this to move the database onto the mounted volume.
        var dbPath = Path.Combine(Path.GetTempPath(), $"td-cfg-{Guid.NewGuid():N}.db");
        try
        {
            using var app = new CustomPathApp(dbPath);
            using var scope = app.Services.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();

            Assert.True(File.Exists(dbPath), $"expected the database at the configured path {dbPath}");
        }
        finally
        {
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    private sealed class CustomPathApp(string dbPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TouchDown"] = $"Data Source={dbPath}"
                }));
        }
    }
}
