using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudBlazor;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.Areas.Drives.Monitor;

public interface IDrivesMonitorPageVM
{
    Drive? Drive { get; }
    Dictionary<string, AgentStatusInfo> AgentStatuses { get; }
    List<LogEntry> Logs { get; }
    string? DriveId { get; set; }
    Task LoadDriveAsync(string driveId);
    void AddLog(LogEntry entry);
    void UpdateAgentStatus(string agentName, string status, int progress);
    void MarkDriveCompleted(string status);
    Task CancelDrive();
}

public class DrivesMonitorPageVMException : Exception
{
    public DrivesMonitorPageVMException() { }
    public DrivesMonitorPageVMException(string message) : base(message) { }
    public DrivesMonitorPageVMException(string message, Exception innerException) : base(message, innerException) { }
}

public partial class DrivesMonitorPageVM : VM, IDrivesMonitorPageVM, IAsyncDisposable
{
    private readonly IDrivesMonitorService _service;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesMonitorPageVM>();

    public DrivesMonitorPageVM(IDrivesMonitorService service)
    {
        _service = service;
    }

    [ObservableProperty]
    private Drive? _drive;

    [ObservableProperty]
    private Dictionary<string, AgentStatusInfo> _agentStatuses = new();

    [ObservableProperty]
    private List<LogEntry> _logs = [];

    public string? DriveId { get; set; }

    public async Task LoadDriveAsync(string driveId)
    {
        _log.Debug("Loading drive {DriveId}", driveId);
        try
        {
            DriveId = driveId;
            Drive = await _service.GetDriveAsync(driveId);

            if (Drive?.AgentTeam?.Members != null)
            {
                var statuses = new Dictionary<string, AgentStatusInfo>();
                foreach (var member in Drive.AgentTeam.Members)
                    statuses[member.Name] = new AgentStatusInfo { Status = "Pending", Progress = 0 };
                AgentStatuses = statuses;
            }

            if (Drive?.Logs != null)
            {
                Logs = Drive.Logs.Select(l => new LogEntry
                {
                    Timestamp = l.Timestamp,
                    AgentName = l.AgentName ?? "System",
                    Message = l.Message,
                    Level = l.Level.ToString()
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load drive {DriveId}", driveId);
            throw new DrivesMonitorPageVMException($"Failed to load drive '{driveId}'", ex);
        }
    }

    public void AddLog(LogEntry entry)
    {
        Logs = [..Logs, entry];
    }

    public void UpdateAgentStatus(string agentName, string status, int progress)
    {
        if (AgentStatuses.ContainsKey(agentName))
        {
            var updated = new Dictionary<string, AgentStatusInfo>(AgentStatuses)
            {
                [agentName] = new AgentStatusInfo { Status = status, Progress = progress }
            };
            AgentStatuses = updated;
        }
    }

    public void MarkDriveCompleted(string status)
    {
        if (Drive != null)
        {
            Drive.Status = status == "Touchdown" ? DriveStatus.Touchdown : DriveStatus.Turnover;
            Drive.CompletedAt = DateTime.UtcNow;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    public async Task CancelDrive()
    {
        _log.Information("Cancelling drive {DriveId}", DriveId);
        try
        {
            if (DriveId != null)
                await _service.CancelDriveAsync(DriveId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to cancel drive {DriveId}", DriveId);
            throw new DrivesMonitorPageVMException($"Failed to cancel drive '{DriveId}'", ex);
        }
    }

    public static Color GetStatusColor(DriveStatus status) => status switch
    {
        DriveStatus.Touchdown => Color.Success,
        DriveStatus.Turnover => Color.Error,
        DriveStatus.InProgress => Color.Info,
        DriveStatus.Huddle => Color.Secondary,
        _ => Color.Default
    };

    public static Color GetAgentStatusColor(string status) => status.ToLower() switch
    {
        "running" => Color.Info,
        "completed" => Color.Success,
        "failed" => Color.Error,
        _ => Color.Default
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class AgentStatusInfo
{
    public string Status { get; set; } = "Pending";
    public int Progress { get; set; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string AgentName { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "Info";
}
