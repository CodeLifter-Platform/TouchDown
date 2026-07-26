using Microsoft.EntityFrameworkCore;
using TD.Data;
using TD.Models;

namespace TD.Services;

/// <summary>
/// Reconciles drives left <see cref="DriveStatus.InProgress"/> by a process that stopped.
///
/// Drive execution is an in-process background task, and the registry of live drives is
/// in-memory, so nothing survives a restart. Any drive still marked InProgress when the
/// app starts is therefore orphaned by definition — no agent is working on it and none
/// ever will. Without this, such a drive sits InProgress until the stale-drive job's
/// 30-minute timeout notices, showing a spinner for work that already died.
/// </summary>
public class OrphanedDriveReconciler
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly ILogger<OrphanedDriveReconciler> _logger;

    public OrphanedDriveReconciler(
        IDbContextFactory<TDDbContext> dbFactory,
        ILogger<OrphanedDriveReconciler> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>Marks every orphaned drive as Turnover. Returns how many were reconciled.</summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var orphaned = await db.Drives
            .Where(d => d.Status == DriveStatus.InProgress)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return 0;

        _logger.LogWarning(
            "Found {Count} drive(s) left InProgress by a previous process; marking as Turnover",
            orphaned.Count);

        foreach (var drive in orphaned)
        {
            drive.Status = DriveStatus.Turnover;
            drive.CompletedAt = DateTime.UtcNow;

            db.DriveLogs.Add(new DriveLog
            {
                AgentName = "System",
                Message = "The application restarted while this drive was running. "
                          + "Drive execution does not survive a restart, so it was marked as Turnover.",
                DriveId = drive.Id,
                Level = Models.LogLevel.Warning
            });
        }

        await db.SaveChangesAsync(ct);
        return orphaned.Count;
    }
}
