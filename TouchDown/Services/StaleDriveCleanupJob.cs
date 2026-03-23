using Microsoft.EntityFrameworkCore;
using TD.Data;
using TD.Models;

namespace TD.Services;

/// <summary>
/// Hangfire recurring job that marks drives as Turnover if they've been InProgress
/// for longer than the timeout threshold (default: 30 minutes).
/// </summary>
public class StaleDriveCleanupJob
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly ILogger<StaleDriveCleanupJob> _logger;
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(30);

    public StaleDriveCleanupJob(
        IDbContextFactory<TDDbContext> dbFactory,
        ILogger<StaleDriveCleanupJob> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cutoff = DateTime.UtcNow - _timeout;
        var staleDrives = await db.Drives
            .Where(d => d.Status == DriveStatus.InProgress && d.CreatedAt < cutoff)
            .ToListAsync();

        if (staleDrives.Count == 0) return;

        _logger.LogWarning("Found {Count} stale drives, marking as Turnover", staleDrives.Count);

        foreach (var drive in staleDrives)
        {
            drive.Status = DriveStatus.Turnover;
            drive.CompletedAt = DateTime.UtcNow;

            db.DriveLogs.Add(new DriveLog
            {
                AgentName = "System",
                Message = $"Drive timed out after {_timeout.TotalMinutes} minutes and was marked as Turnover.",
                DriveId = drive.Id,
                Level = Models.LogLevel.Warning
            });
        }

        await db.SaveChangesAsync();
    }
}
