using Serilog;
using TD.Models;
using TD.Services;

namespace TD.Areas.Drives.Monitor;

public interface IDrivesMonitorService
{
    Task<Drive?> GetDriveAsync(string driveId);
    Task CancelDriveAsync(string driveId);
    Task RenameDriveAsync(string driveId, string? name);
}

public class DrivesMonitorServiceException : Exception
{
    public DrivesMonitorServiceException() { }
    public DrivesMonitorServiceException(string message) : base(message) { }
    public DrivesMonitorServiceException(string message, Exception innerException) : base(message, innerException) { }
}

public class DrivesMonitorService : IDrivesMonitorService
{
    private readonly IDrivesMonitorServiceDA _da;
    private readonly IAgentOrchestrationService _orchestration;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesMonitorService>();

    public DrivesMonitorService(IDrivesMonitorServiceDA da, IAgentOrchestrationService orchestration)
    {
        _da = da;
        _orchestration = orchestration;
    }

    public async Task<Drive?> GetDriveAsync(string driveId)
    {
        _log.Debug("Getting drive {DriveId}", driveId);
        try
        {
            return await _da.GetDriveAsync(driveId);
        }
        catch (Exception ex) when (ex is not DrivesMonitorServiceException)
        {
            _log.Error(ex, "Failed to get drive {DriveId}", driveId);
            throw new DrivesMonitorServiceException($"Failed to get drive '{driveId}'", ex);
        }
    }

    public async Task RenameDriveAsync(string driveId, string? name)
    {
        _log.Debug("Renaming drive {DriveId}", driveId);
        try
        {
            await _da.RenameDriveAsync(driveId, name);
        }
        catch (Exception ex) when (ex is not DrivesMonitorServiceException)
        {
            _log.Error(ex, "Failed to rename drive {DriveId}", driveId);
            throw new DrivesMonitorServiceException($"Failed to rename drive '{driveId}'", ex);
        }
    }

    public async Task CancelDriveAsync(string driveId)
    {
        _log.Information("Cancelling drive {DriveId}", driveId);
        try
        {
            await _orchestration.CancelDriveAsync(driveId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to cancel drive {DriveId}", driveId);
            throw new DrivesMonitorServiceException($"Failed to cancel drive '{driveId}'", ex);
        }
    }
}
