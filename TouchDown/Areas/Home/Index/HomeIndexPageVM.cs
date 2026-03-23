using CommunityToolkit.Mvvm.ComponentModel;
using MudBlazor;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.Areas.Home.Index;

public interface IHomeIndexPageVM
{
    List<Drive> RecentDrives { get; }
    ClaudeHealthStatus? HealthStatus { get; }
    Task Loaded();
}

public class HomeIndexPageVMException : Exception
{
    public HomeIndexPageVMException() { }
    public HomeIndexPageVMException(string message) : base(message) { }
    public HomeIndexPageVMException(string message, Exception innerException) : base(message, innerException) { }
}

public partial class HomeIndexPageVM : VM, IHomeIndexPageVM
{
    private readonly IHomeIndexService _service;
    private readonly Serilog.ILogger _log = Log.ForContext<HomeIndexPageVM>();

    public HomeIndexPageVM(IHomeIndexService service)
    {
        _service = service;
    }

    [ObservableProperty]
    private List<Drive> _recentDrives = [];

    [ObservableProperty]
    private ClaudeHealthStatus? _healthStatus;

    public override async Task Loaded()
    {
        _log.Debug("Loading Home Index page");
        try
        {
            HealthStatus = await _service.GetHealthStatusAsync();
            RecentDrives = await _service.GetRecentDrivesAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load Home Index page");
            throw new HomeIndexPageVMException("Failed to load Home Index page", ex);
        }
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
