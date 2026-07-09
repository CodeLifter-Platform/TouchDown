using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudBlazor;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.Areas.Drives.Monitor;

public interface IDrivesMonitorPageVM
{
    Drive? Drive { get; }
    Dictionary<string, AgentStatusInfo> AgentStatuses { get; }
    List<LogEntry> Logs { get; }
    List<PlaySummaryVM> Plays { get; }
    List<TurnVM> Turns { get; }
    string CurrentPhase { get; }
    string? DriveId { get; set; }
    Task LoadDriveAsync(string driveId);
    void AddLog(LogEntry entry);
    void UpdateAgentStatus(string agentName, string status, int progress);
    void MarkDriveCompleted(string status);
    void SetPhase(string phase);
    void SetPlays(List<PlaySummaryVM> plays);
    void UpdatePlayStatus(int playId, string status, DateTime? startedAt, DateTime? completedAt);
    Task CancelDrive();
    Task RenameDrive(string? name);
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

    [ObservableProperty]
    private List<PlaySummaryVM> _plays = [];

    [ObservableProperty]
    private List<TurnVM> _turns = [];

    [ObservableProperty]
    private string _currentPhase = "Starting";

    public string? DriveId { get; set; }

    public async Task LoadDriveAsync(string driveId)
    {
        _log.Debug("Loading drive {DriveId}", driveId);
        try
        {
            DriveId = driveId;
            Drive = await _service.GetDriveAsync(driveId);

            // Label fan-out instances ("The Offensive Line #1", "#2", …) the same way the orchestrator does.
            var instanceLabels = Drive?.Plays is { Count: > 0 } labelPlays
                ? InstanceLabeler.Label(labelPlays.Select(p => new InstanceLabeler.PlayRef(
                    p.Id, p.AssignedMemberId, p.AssignedMember?.Name ?? "Unknown", p.OrderIndex, p.AssignedMember?.MaxInstances ?? 1)))
                : new Dictionary<int, string>();

            if (Drive?.AgentTeam?.Members != null)
            {
                var statuses = new Dictionary<string, AgentStatusInfo>();
                // Single-instance members each get one card; fan-out members surface as per-instance cards.
                foreach (var member in Drive.AgentTeam.Members.Where(m => m.MaxInstances <= 1))
                    statuses[member.Name] = new AgentStatusInfo { Status = "Pending", Progress = 0 };
                // A card per play instance — shows fan-out instances and reflects their status on replay.
                if (Drive.Plays != null)
                    foreach (var p in Drive.Plays)
                        statuses[instanceLabels.GetValueOrDefault(p.Id, p.AssignedMember?.Name ?? "Unknown")] = PlayStatusToInfo(p.Status);
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

            if (Drive?.Turns is { Count: > 0 })
            {
                Turns = Drive.Turns
                    .OrderBy(t => t.Timestamp).ThenBy(t => t.Id)
                    .Select(t => new TurnVM
                    {
                        Phase = t.Phase,
                        Role = t.Role,
                        AgentName = t.AgentName ?? "",
                        Content = t.Content,
                        ToolsUsed = t.ToolsUsed,
                        CostUsd = t.CostUsd,
                        Timestamp = t.Timestamp
                    }).ToList();
            }

            if (Drive?.Plays is { Count: > 0 })
            {
                Plays = Drive.Plays
                    .OrderBy(p => p.OrderIndex)
                    .Select(p => new PlaySummaryVM
                    {
                        Id = p.Id,
                        AgentName = instanceLabels.GetValueOrDefault(p.Id, p.AssignedMember?.Name ?? "Unknown"),
                        Description = p.Description,
                        Status = p.Status,
                        StartedAt = p.StartedAt,
                        CompletedAt = p.CompletedAt,
                        OrderIndex = p.OrderIndex,
                        Output = p.Output
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
        // Add-or-update so fan-out instances ("The Offensive Line #3") appear as they fire off.
        AgentStatuses = new Dictionary<string, AgentStatusInfo>(AgentStatuses)
        {
            [agentName] = new AgentStatusInfo { Status = status, Progress = progress }
        };
    }

    private static AgentStatusInfo PlayStatusToInfo(PlayStatus status) => status switch
    {
        PlayStatus.Completed => new() { Status = "Completed", Progress = 100 },
        PlayStatus.Failed => new() { Status = "Failed", Progress = 100 },
        PlayStatus.InProgress => new() { Status = "Running", Progress = 50 },
        PlayStatus.Skipped => new() { Status = "Skipped", Progress = 100 },
        _ => new() { Status = "Pending", Progress = 0 }
    };

    public void SetPhase(string phase) => CurrentPhase = phase;

    public void SetPlays(List<PlaySummaryVM> plays) => Plays = plays;

    public void UpdatePlayStatus(int playId, string status, DateTime? startedAt, DateTime? completedAt)
    {
        var playStatus = Enum.TryParse<PlayStatus>(status, out var s) ? s : PlayStatus.Pending;
        Plays = Plays.Select(p => p.Id == playId
            ? p with { Status = playStatus, StartedAt = startedAt, CompletedAt = completedAt }
            : p).ToList();
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
    public async Task RenameDrive(string? name)
    {
        if (DriveId == null || Drive == null) return;
        _log.Information("Renaming drive {DriveId}", DriveId);
        try
        {
            await _service.RenameDriveAsync(DriveId, name);
            Drive.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to rename drive {DriveId}", DriveId);
            throw new DrivesMonitorPageVMException($"Failed to rename drive '{DriveId}'", ex);
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

public record TurnVM
{
    public TurnPhase Phase { get; init; }
    public string Role { get; init; } = "";
    public string AgentName { get; init; } = "";
    public string Content { get; init; } = "";
    public string? ToolsUsed { get; init; }
    public double? CostUsd { get; init; }
    public DateTime Timestamp { get; init; }
}

public record PlaySummaryVM
{
    public int Id { get; init; }
    public string AgentName { get; init; } = "";
    public string Description { get; init; } = "";
    public PlayStatus Status { get; init; } = PlayStatus.Pending;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int OrderIndex { get; init; }
    public string? Output { get; init; }

    public TimeSpan? Duration =>
        StartedAt.HasValue && CompletedAt.HasValue ? CompletedAt - StartedAt : null;
}
