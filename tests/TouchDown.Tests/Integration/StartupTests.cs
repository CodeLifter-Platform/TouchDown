using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TD.Data;
using TD.Services;
using TouchDown.Tests.TestSupport;

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
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Application_StartsAndServesRequests(string environment)
    {
        // Regression: this threw "JobStorage instance has not been initialized" in
        // Production and the process died before binding a port.
        using var app = new HostedApp(environment);

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
        using var app = new HostedApp(environment);

        // Forcing the host to build runs the whole startup path, including job registration.
        var jobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        Assert.NotNull(jobManager);
    }

    [Fact]
    public async Task Startup_AppliesMigrationsAndSeedsTheDefaultTeam()
    {
        using var app = new HostedApp("Production");

        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(await db.AgentTeams.ToListAsync(), t => t.Name == "The Playbook");
    }

    [Fact]
    public void Startup_RegistersBothAgentProviders()
    {
        using var app = new HostedApp("Production");

        var registry = app.Services.GetRequiredService<IAgentProviderRegistry>();

        Assert.Contains(registry.All, p => p.ProviderId == "claude-code");
        Assert.Contains(registry.All, p => p.ProviderId == "codex");
    }

    [Fact]
    public void Startup_RegistersTheOrphanedDriveReconciler()
    {
        using var app = new HostedApp("Production");

        using var scope = app.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrphanedDriveReconciler>());
    }

    [Fact]
    public void Startup_UsesDurableJobStorage()
    {
        // Memory storage loses recurring schedules and job history on every restart.
        using var app = new HostedApp("Production");

        var storage = app.Services.GetRequiredService<JobStorage>();

        Assert.Contains("SQLite", storage.GetType().Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_PersistsTheRecurringCleanupJob()
    {
        // The schedule lives in job storage now, so it should be readable back out.
        using var app = new HostedApp("Production");
        _ = app.Services.GetRequiredService<IRecurringJobManager>();

        var jobs = app.Services.GetRequiredService<JobStorage>()
            .GetMonitoringApi()
            .GetStatistics();

        Assert.NotNull(jobs);
        Assert.True(jobs.Recurring >= 1, $"expected at least one persisted recurring job, saw {jobs.Recurring}");
    }

    [Fact]
    public async Task ConnectionStringConfiguration_IsHonoured()
    {
        // The Dockerfile relies on this to move the database onto the mounted volume.
        using var app = new HostedApp();
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        Assert.True(File.Exists(app.DatabasePath), $"expected the database at the configured path {app.DatabasePath}");
    }
}
