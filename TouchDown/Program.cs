using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using TD.Application;
using TD.Data;
using TD.Hubs;
using TD.Services;

var builder = WebApplication.CreateBuilder(args);

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

// EF Core with IDbContextFactory pattern
builder.Services.AddDbContextFactory<TDDbContext>(options =>
    options.UseSqlite("Data Source=touchdown.db"));

// Hangfire
builder.Services.AddHangfire(config =>
    config.UseMemoryStorage());
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

// Application services
builder.Services.AddSingleton<IClaudeStreamingService, ClaudeStreamingService>(); // kept for ClaudeCodeProvider
builder.Services.AddAgentProvider<ClaudeCodeProvider>();                           // registers IAgentProvider + ClaudeCodeProvider
builder.Services.AddAgentProvider<CodexProvider>();                                // registers IAgentProvider + CodexProvider
builder.Services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
builder.Services.AddSingleton<ISharedDriveContext, SharedDriveContext>();
builder.Services.AddSingleton<IPlanParserService, PlanParserService>();
builder.Services.AddSingleton<IAgentOrchestrationService, AgentOrchestrationService>();
builder.Services.AddTransient<StaleDriveCleanupJob>();

var app = builder.Build();

// Ensure database is created with seed data
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TDDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
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

// Hangfire dashboard (dev only) + recurring jobs
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
}

// Stale drive cleanup every 5 minutes
RecurringJob.AddOrUpdate<StaleDriveCleanupJob>(
    "stale-drive-cleanup",
    job => job.ExecuteAsync(),
    "*/5 * * * *");

app.Run();
