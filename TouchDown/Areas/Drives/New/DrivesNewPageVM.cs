using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.Areas.Drives.New;

public interface IDrivesNewPageVM
{
    int CurrentStep { get; set; }
    AgentSession Session { get; }
    bool ShowHuddle { get; }
    bool CanStartHuddle { get; }
    bool CanSkipHuddle { get; }
    string MissingStepsText { get; }
    string SourceDisplayName { get; }
    string ModeDisplayName { get; }
    bool WorkspaceNeedsGitInit { get; }
    void RefreshCanStart();
    void StartHuddle();
    Task<string?> SnapDirectly();
    void CloseHuddle();
}

public class DrivesNewPageVMException : Exception
{
    public DrivesNewPageVMException() { }
    public DrivesNewPageVMException(string message) : base(message) { }
    public DrivesNewPageVMException(string message, Exception innerException) : base(message, innerException) { }
}

public partial class DrivesNewPageVM : VM, IDrivesNewPageVM
{
    private readonly IDrivesNewService _service;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesNewPageVM>();

    public DrivesNewPageVM(IDrivesNewService service)
    {
        _service = service;
    }

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private AgentSession _session = new();

    [ObservableProperty]
    private bool _showHuddle;

    public bool CanStartHuddle =>
        !string.IsNullOrWhiteSpace(Session.RepoPath) &&
        Session.Team != null &&
        !string.IsNullOrWhiteSpace(Session.TaskDescription);

    public bool CanSkipHuddle => CanStartHuddle;

    public string MissingStepsText
    {
        get
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(Session.RepoPath)) missing.Add("source");
            if (Session.Team == null) missing.Add("team");
            if (string.IsNullOrWhiteSpace(Session.TaskDescription)) missing.Add("task description");
            return $"Complete the setup first — missing: {string.Join(", ", missing)}.";
        }
    }

    public bool WorkspaceNeedsGitInit
    {
        get
        {
            if (Session.WorkspaceMode != WorkspaceMode.FreshFolder)
                return false;
            var path = Session.RepoPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;
            return !Directory.Exists(Path.Combine(path, ".git"));
        }
    }

    public string SourceDisplayName => Path.GetFileName(Session.RepoPath ?? "—");
    public string ModeDisplayName => Session.WorkspaceMode == WorkspaceMode.PrWorktree ? "Worktree → PR" : "Direct on branch";

    public void RefreshCanStart() => NotifyStateChanged();

    [RelayCommand]
    public void StartHuddle()
    {
        _log.Debug("Starting huddle");
        Session.Drive = new Drive();
        ShowHuddle = true;
    }

    [RelayCommand]
    public async Task<string?> SnapDirectly()
    {
        _log.Information("Snapping directly without huddle");
        try
        {
            Session.Drive = new Drive();
            var drive = await _service.StartDriveAsync(Session);
            return drive.DriveId;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to snap directly");
            throw new DrivesNewPageVMException("Failed to snap directly", ex);
        }
    }

    public void CloseHuddle() => ShowHuddle = false;
}
