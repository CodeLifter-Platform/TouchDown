using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Drives.Monitor;

public interface IDrivesMonitorServiceDA
{
    Task<Drive?> GetDriveAsync(string driveId);
    Task RenameDriveAsync(string driveId, string? name);
}

public class DrivesMonitorServiceDAException : Exception
{
    public DrivesMonitorServiceDAException() { }
    public DrivesMonitorServiceDAException(string message) : base(message) { }
    public DrivesMonitorServiceDAException(string message, Exception innerException) : base(message, innerException) { }
}

public class DrivesMonitorServiceDA : IDrivesMonitorServiceDA
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesMonitorServiceDA>();

    public DrivesMonitorServiceDA(IDbContextFactory<TDDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Drive?> GetDriveAsync(string driveId)
    {
        _log.Debug("Fetching drive {DriveId}", driveId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Drives
                .Include(d => d.AgentTeam)
                    .ThenInclude(t => t!.Members)
                .Include(d => d.Logs)
                .Include(d => d.Plays)
                    .ThenInclude(p => p.AssignedMember)
                .Include(d => d.Turns)
                .FirstOrDefaultAsync(d => d.DriveId == driveId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to fetch drive {DriveId}", driveId);
            throw new DrivesMonitorServiceDAException($"Failed to fetch drive '{driveId}'", ex);
        }
    }

    public async Task RenameDriveAsync(string driveId, string? name)
    {
        _log.Debug("Renaming drive {DriveId}", driveId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var drive = await db.Drives.FirstOrDefaultAsync(d => d.DriveId == driveId);
            if (drive != null)
            {
                drive.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to rename drive {DriveId}", driveId);
            throw new DrivesMonitorServiceDAException($"Failed to rename drive '{driveId}'", ex);
        }
    }
}
