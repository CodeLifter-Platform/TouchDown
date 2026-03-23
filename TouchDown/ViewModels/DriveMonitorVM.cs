using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudBlazor;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.ViewModels;

public partial class DriveMonitorVM : VM, IAsyncDisposable
{
    private readonly IAgentOrchestrationService _orchestration;

    public DriveMonitorVM(IAgentOrchestrationService orchestration)
    {
        _orchestration = orchestration;
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
        DriveId = driveId;
        Drive = await _orchestration.GetDriveAsync(driveId);

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
        if (DriveId != null)
            await _orchestration.CancelDriveAsync(DriveId);
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
}
