using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using Serilog;
using TD.Application;
using TD.Data;
using TD.Hubs;
using TD.Models;
using TD.Services;
using TD.Services.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
// Services and VMs log through the static `Log.ForContext<T>()` API, so the static
// logger must be assigned or all of that output goes to a silent no-op sink.
// UseSerilog() with no argument then routes Microsoft's ILogger<T> through the same
// pipeline, giving one sink set for both.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});

// SignalR
builder.Services.AddSignalR();

// EF Core with IDbContextFactory pattern.
// The connection string comes from configuration so a deployment can point the DB at a
// mounted volume; the container image sets ConnectionStrings__TouchDown to /app/data.
builder.Services.AddDbContextFactory<TDDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TouchDown")
                      ?? "Data Source=touchdown.db"));

// Hangfire — SQLite storage rather than in-memory, so recurring job schedules and job
// history survive a restart. Its file sits alongside the app database (the container
// points both at the mounted volume).
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(builder.Configuration.GetConnectionString("Hangfire") ?? "hangfire.db"));
builder.Services.AddHangfireServer();

// Health checks
builder.Services.AddSingleton<ClaudeHealthCheck>();
builder.Services.AddSingleton<IClaudeHealthCheck>(sp => sp.GetRequiredService<ClaudeHealthCheck>());
builder.Services.AddSingleton<CodexHealthCheck>();
builder.Services.AddSingleton<ICodexHealthCheck>(sp => sp.GetRequiredService<CodexHealthCheck>());
builder.Services.AddHealthChecks()
    .AddCheck<ClaudeHealthCheck>("claude-code")
    .AddCheck<CodexHealthCheck>("codex");

// Area: Home/Index
builder.Services.AddScoped<TD.Areas.Home.Index.IHomeIndexServiceDA, TD.Areas.Home.Index.HomeIndexServiceDA>();
builder.Services.AddScoped<TD.Areas.Home.Index.IHomeIndexService, TD.Areas.Home.Index.HomeIndexService>();
builder.Services.AddScoped<TD.Areas.Home.Index.HomeIndexPageVM>();

// Area: Teams/Index
builder.Services.AddScoped<TD.Areas.Teams.Index.ITeamsIndexServiceDA, TD.Areas.Teams.Index.TeamsIndexServiceDA>();
builder.Services.AddScoped<TD.Areas.Teams.Index.ITeamsIndexService, TD.Areas.Teams.Index.TeamsIndexService>();
builder.Services.AddScoped<TD.Areas.Teams.Index.TeamsIndexPageVM>();

// Area: Drives/New
builder.Services.AddScoped<TD.Areas.Drives.New.IDrivesNewServiceDA, TD.Areas.Drives.New.DrivesNewServiceDA>();
builder.Services.AddScoped<TD.Areas.Drives.New.IDrivesNewService, TD.Areas.Drives.New.DrivesNewService>();
builder.Services.AddScoped<TD.Areas.Drives.New.DrivesNewPageVM>();
builder.Services.AddScoped<TD.Areas.Drives.New.HuddleVM>();

// Area: Drives/Monitor
builder.Services.AddScoped<TD.Areas.Drives.Monitor.IDrivesMonitorServiceDA, TD.Areas.Drives.Monitor.DrivesMonitorServiceDA>();
builder.Services.AddScoped<TD.Areas.Drives.Monitor.IDrivesMonitorService, TD.Areas.Drives.Monitor.DrivesMonitorService>();
builder.Services.AddScoped<TD.Areas.Drives.Monitor.DrivesMonitorPageVM>();

// User preferences (singleton — shared in-memory state across all components)
builder.Services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

// Telemetry (scoped per-circuit; NullTelemetryService sends nothing — swap class when backend chosen)
builder.Services.AddScoped<ITelemetryService, NullTelemetryService>();

// Application services
builder.Services.AddSingleton<IClaudeStreamingService, ClaudeStreamingService>(); // kept for ClaudeCodeProvider
builder.Services.AddAgentProvider<ClaudeCodeProvider>();                           // registers IAgentProvider + ClaudeCodeProvider
builder.Services.AddAgentProvider<CodexProvider>();                                // registers IAgentProvider + CodexProvider
builder.Services.AddSingleton<IAgentProviderRegistry, AgentProviderRegistry>();
builder.Services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
builder.Services.AddSingleton<ISharedDriveContext, SharedDriveContext>();
builder.Services.AddSingleton<IPlanParserService, PlanParserService>();
builder.Services.AddSingleton<IAgentOrchestrationService, AgentOrchestrationService>();
builder.Services.AddTransient<StaleDriveCleanupJob>();
builder.Services.AddTransient<OrphanedDriveReconciler>();

var app = builder.Build();

// Apply pending migrations (handles legacy EnsureCreated DBs)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    // If the DB has tables but no migration history (EnsureCreated legacy),
    // seed the history so EF skips InitialCreate and only runs newer migrations.
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using (var cmd = conn.CreateCommand())
    {
        // Check if tables exist but InitialCreate hasn't been recorded
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AgentTeams';";
        var tablesExist = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;

        if (tablesExist)
        {
            // Ensure history table exists
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync();

            // Mark InitialCreate as applied if not already
            cmd.CommandText = """
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20260323205938_InitialCreate', '10.0.5');
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    await conn.CloseAsync();
    await db.Database.MigrateAsync();
}

// Close out drives orphaned by a previous process — execution is in-memory, so anything
// still InProgress at startup is dead and would otherwise spin until the 30-minute sweep.
using (var scope = app.Services.CreateScope())
{
    var reconciler = scope.ServiceProvider.GetRequiredService<OrphanedDriveReconciler>();
    await reconciler.ReconcileAsync();
}

// Run Claude health check on startup
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var healthCheck = app.Services.GetRequiredService<IClaudeHealthCheck>();
    logger.LogInformation("Running Claude Code health check...");
    var status = await healthCheck.CheckAsync();

    if (status.IsHealthy)
    {
        logger.LogInformation("Claude Code is ready: v{Version} at {Path}", status.Version, status.CliPath);
    }
    else if (status.IsInstalled && !status.IsAuthenticated)
    {
        logger.LogWarning("Claude Code is installed but NOT authenticated. Run 'claude auth' to set up your license. Error: {Error}", status.Error);
    }
    else
    {
        logger.LogError("Claude Code CLI not found! Install from https://docs.anthropic.com/en/docs/claude-code. Error: {Error}", status.Error);
    }
}

// Run Codex health check on startup
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var healthCheck = app.Services.GetRequiredService<ICodexHealthCheck>();
    logger.LogInformation("Running Codex CLI health check...");
    var status = await healthCheck.CheckAsync();

    if (status.IsHealthy)
    {
        logger.LogInformation("Codex CLI is ready: v{Version} at {Path}", status.Version, status.CliPath);
    }
    else if (status.IsInstalled && !status.IsAuthenticated)
    {
        logger.LogWarning("Codex CLI is installed but NOT authenticated. Run 'codex auth' to log in. Error: {Error}", status.Error);
    }
    else
    {
        logger.LogWarning("Codex CLI not found — Codex provider will be unavailable. Install via 'npm i -g @openai/codex'. Error: {Error}", status.Error);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health endpoint
app.MapHealthChecks("/health");

// SignalR hub
app.MapHub<AgentHub>("/agentHub");

// Hangfire dashboard. Off outside Development by default, and loopback-only wherever it
// is on — the dashboard can trigger, requeue and delete jobs, and the app has no auth.
if (app.Configuration.GetValue("Hangfire:EnableDashboard", app.Environment.IsDevelopment()))
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new LocalRequestsOnlyDashboardFilter()]
    });
}

// Stale drive cleanup every 5 minutes.
// Registered through IRecurringJobManager rather than the static RecurringJob API: the
// static one reads JobStorage.Current, which is only initialized as a side effect of
// resolving Hangfire from DI. MapHangfireDashboard did that — but it is Development-only,
// so in Production the static call threw and the app never started.
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<StaleDriveCleanupJob>(
        "stale-drive-cleanup",
        job => job.ExecuteAsync(),
        "*/5 * * * *");
}

app.Run();

/// <summary>
/// Exposed so integration tests can boot the real application through
/// <c>WebApplicationFactory</c>. Top-level statements generate an internal Program class;
/// this partial makes it public without changing any behaviour.
/// </summary>
public partial class Program;
