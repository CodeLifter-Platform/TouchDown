using TD.Areas.Drives.New;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.ViewModels;

/// <summary>
/// The New Drive wizard's gating: the huddle and the snap are unavailable until a source, a
/// team and a task exist, the missing pieces are named, and a fresh folder that is not yet a
/// git repo is flagged before the drive tries to use it.
/// </summary>
public class DrivesNewPageVMTests
{
    private static DrivesNewPageVM Create(ScriptedDrivesNewService? service = null) =>
        new(service ?? new ScriptedDrivesNewService());

    private static AgentSession CompleteSession(string repoPath = "/tmp/repo") => new()
    {
        RepoPath = repoPath,
        Team = new AgentTeam { Id = 1, Name = "The Playbook" },
        TaskDescription = "Add a login page",
    };

    [Fact]
    public void Nothing_can_start_until_source_team_and_task_are_all_set()
    {
        var vm = Create();

        Assert.False(vm.CanStartHuddle);
        Assert.False(vm.CanSkipHuddle);
        Assert.Contains("source", vm.MissingStepsText);
        Assert.Contains("team", vm.MissingStepsText);
        Assert.Contains("task description", vm.MissingStepsText);
    }

    [Fact]
    public void The_missing_steps_text_names_only_what_is_missing()
    {
        var vm = Create();
        vm.Session = CompleteSession();
        vm.Session.TaskDescription = "  ";

        Assert.False(vm.CanStartHuddle);
        Assert.Equal("Complete the setup first — missing: task description.", vm.MissingStepsText);
    }

    [Fact]
    public void A_complete_session_can_start_the_huddle_or_skip_it()
    {
        var vm = Create();
        vm.Session = CompleteSession();

        Assert.True(vm.CanStartHuddle);
        Assert.True(vm.CanSkipHuddle);
    }

    [Fact]
    public void A_fresh_folder_without_a_git_repo_needs_initialising()
    {
        using var folder = TempRepo.CreateEmptyDirectory();
        var vm = Create();
        vm.Session = CompleteSession(folder.Path);
        vm.Session.WorkspaceMode = WorkspaceMode.FreshFolder;

        Assert.True(vm.WorkspaceNeedsGitInit);
    }

    [Fact]
    public void A_fresh_folder_that_is_already_a_repo_does_not()
    {
        using var repo = TempRepo.CreateGitRepo();
        var vm = Create();
        vm.Session = CompleteSession(repo.Path);
        vm.Session.WorkspaceMode = WorkspaceMode.FreshFolder;

        Assert.False(vm.WorkspaceNeedsGitInit);
    }

    [Fact]
    public void Only_the_fresh_folder_mode_asks_about_git_init()
    {
        using var folder = TempRepo.CreateEmptyDirectory();
        var vm = Create();
        vm.Session = CompleteSession(folder.Path);
        vm.Session.WorkspaceMode = WorkspaceMode.CurrentBranch;

        Assert.False(vm.WorkspaceNeedsGitInit);
    }

    [Fact]
    public void A_folder_that_does_not_exist_yet_is_not_flagged()
    {
        var vm = Create();
        vm.Session = CompleteSession(Path.Combine(Path.GetTempPath(), "td-does-not-exist-" + Guid.NewGuid().ToString("N")));
        vm.Session.WorkspaceMode = WorkspaceMode.FreshFolder;

        Assert.False(vm.WorkspaceNeedsGitInit);
    }

    [Fact]
    public async Task Snapping_directly_starts_a_fresh_drive_and_returns_its_id()
    {
        var service = new ScriptedDrivesNewService { StartedDriveId = "drive-42" };
        var vm = Create(service);
        vm.Session = CompleteSession();

        var id = await vm.SnapDirectly();

        Assert.Equal("drive-42", id);
        Assert.Same(vm.Session, service.StartedWith);
        Assert.Equal(0, vm.Session.Drive.Id);
    }

    [Fact]
    public async Task A_snap_that_fails_is_reported_to_the_page_as_a_page_error()
    {
        var service = new ScriptedDrivesNewService { StartFailure = new DrivesNewServiceException("no providers") };
        var vm = Create(service);
        vm.Session = CompleteSession();

        var ex = await Assert.ThrowsAsync<DrivesNewPageVMException>(() => vm.SnapDirectly());

        Assert.Contains("no providers", ex.InnerException?.Message);
    }

    [Fact]
    public void Starting_the_huddle_opens_it_with_a_new_drive_on_the_session()
    {
        var vm = Create();
        vm.Session = CompleteSession();

        vm.StartHuddle();

        Assert.True(vm.ShowHuddle);
        Assert.NotNull(vm.Session.Drive);
        vm.CloseHuddle();
        Assert.False(vm.ShowHuddle);
    }

    /// <summary>Only what the wizard page itself calls is scripted; everything else fails the test.</summary>
    private sealed class ScriptedDrivesNewService : IDrivesNewService
    {
        public string StartedDriveId { get; init; } = "drive-1";
        public Exception? StartFailure { get; init; }
        public AgentSession? StartedWith { get; private set; }

        public Task<Drive> StartDriveAsync(AgentSession session)
        {
            StartedWith = session;
            if (StartFailure is not null) throw StartFailure;
            return Task.FromResult(new Drive { Id = 1, DriveId = StartedDriveId });
        }

        public Task<List<AvailableProvider>> GetAvailableProvidersAsync() => throw new NotSupportedException();
        public Task<List<AgentTeam>> GetAvailableTeamsAsync() => throw new NotSupportedException();
        public Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team) => throw new NotSupportedException();
        public Task<Drive> CreateDraftDriveAsync(AgentSession session) => throw new NotSupportedException();
        public Task AddTurnAsync(DriveTurn turn) => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamQbResponseAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamCoordinatorResearchAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
