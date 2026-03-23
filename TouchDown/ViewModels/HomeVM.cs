using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudBlazor;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.ViewModels;

public partial class HomeVM : VM
{
    private readonly IAgentOrchestrationService _orchestration;
    private readonly IClaudeHealthCheck _healthCheck;

    public HomeVM(IAgentOrchestrationService orchestration, IClaudeHealthCheck healthCheck)
    {
        _orchestration = orchestration;
        _healthCheck = healthCheck;
    }

    [ObservableProperty]
    private List<Drive> _recentDrives = [];

    [ObservableProperty]
    private ClaudeHealthStatus? _healthStatus;

    public override async Task Loaded()
    {
        HealthStatus = _healthCheck.LastStatus ?? await _healthCheck.CheckAsync();
        RecentDrives = await _orchestration.GetRecentDrivesAsync();
    }

    public static string TruncateTask(string task) =>
        task.Length > 80 ? task[..77] + "..." : task;

    public static Color GetStatusColor(DriveStatus status) => status switch
    {
        DriveStatus.Touchdown => Color.Success,
        DriveStatus.Turnover => Color.Error,
        DriveStatus.InProgress => Color.Info,
        DriveStatus.Huddle => Color.Secondary,
        DriveStatus.Cancelled => Color.Default,
        _ => Color.Default
    };
}
