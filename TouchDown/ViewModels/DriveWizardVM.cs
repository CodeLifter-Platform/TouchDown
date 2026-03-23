using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.ViewModels;

public partial class DriveWizardVM : VM
{
    private readonly IAgentOrchestrationService _orchestration;

    public DriveWizardVM(IAgentOrchestrationService orchestration)
    {
        _orchestration = orchestration;
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

    public string SourceDisplayName => Path.GetFileName(Session.RepoPath ?? "—");
    public string ModeDisplayName => Session.WorkspaceMode == WorkspaceMode.PrWorktree ? "Worktree → PR" : "Direct on branch";

    public void RefreshCanStart() => NotifyStateChanged();

    [RelayCommand]
    public void StartHuddle()
    {
        Session.Drive = new Drive();
        ShowHuddle = true;
    }

    [RelayCommand]
    public async Task<string?> SnapDirectly()
    {
        Session.Drive = new Drive();
        var drive = await _orchestration.StartDriveAsync(Session);
        return drive.DriveId;
    }

    public void CloseHuddle() => ShowHuddle = false;
}
