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
builder.Services.AddHealthChecks()
    .AddCheck<ClaudeHealthCheck>("claude-code");

// ViewModels (transient — each component gets its own instance)
builder.Services.AddTransient<TD.ViewModels.HomeVM>();
builder.Services.AddTransient<TD.ViewModels.DriveWizardVM>();
builder.Services.AddTransient<TD.ViewModels.TeamsVM>();
builder.Services.AddTransient<TD.ViewModels.DriveMonitorVM>();
builder.Services.AddTransient<TD.ViewModels.HuddleVM>();

// Application services
builder.Services.AddSingleton<IClaudeStreamingService, ClaudeStreamingService>();
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
