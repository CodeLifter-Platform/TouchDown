using TD.Areas.Drives.Monitor;
using TD.Models;

namespace TouchDown.Tests.ViewModels;

/// <summary>
/// The monitor page's view of a drive: what it rebuilds from the database on load, and how
/// it folds the live hub messages into the board. Fan-out instances must show as their own
/// cards both on replay and as they fire, and a finished drive must be marked by the status
/// the hub actually sent.
/// </summary>
public class DrivesMonitorPageVMTests
{
    private static readonly AgentMember Line = new() { Id = 2, Name = "The Offensive Line", Role = AgentRole.Worker, MaxInstances = 3 };
    private static readonly AgentMember Safety = new() { Id = 3, Name = "The Safety", Role = AgentRole.Validator, MaxInstances = 1 };

    private static Drive StoredDrive()
    {
        var team = new AgentTeam { Id = 1, Name = "The Playbook", Members = [Line, Safety] };
        var drive = new Drive { Id = 10, DriveId = "drive-10", TaskDescription = "ship it", Status = DriveStatus.InProgress, AgentTeam = team, AgentTeamId = 1 };
        drive.Plays.AddRange(
        [
            new Play { Id = 1, Description = "slice a", OrderIndex = 0, AssignedMemberId = 2, AssignedMember = Line, Status = PlayStatus.Completed },
            new Play { Id = 2, Description = "slice b", OrderIndex = 1, AssignedMemberId = 2, AssignedMember = Line, Status = PlayStatus.InProgress },
            new Play { Id = 3, Description = "review", OrderIndex = 2, AssignedMemberId = 3, AssignedMember = Safety, Status = PlayStatus.Pending },
        ]);
        drive.Logs.Add(new DriveLog { AgentName = "System", Message = "Drive started", Level = TD.Models.LogLevel.Info });
        drive.Turns.Add(new DriveTurn { Phase = TurnPhase.Huddle, Role = "user", AgentName = "Head Coach", Content = "ship it" });
        return drive;
    }

    private static async Task<(DrivesMonitorPageVM VM, ScriptedMonitorService Service)> LoadedAsync(Drive? drive)
    {
        var service = new ScriptedMonitorService { Drive = drive };
        var vm = new DrivesMonitorPageVM(service);
        await vm.LoadDriveAsync("drive-10");
        return (vm, service);
    }

    [Fact]
    public async Task Replaying_a_drive_shows_one_card_per_fan_out_instance_and_one_per_single_member()
    {
        var (vm, _) = await LoadedAsync(StoredDrive());

        Assert.Equal(["The Offensive Line #1", "The Offensive Line #2", "The Safety"], vm.AgentStatuses.Keys.Order());
        Assert.Equal("Completed", vm.AgentStatuses["The Offensive Line #1"].Status);
        Assert.Equal("Running", vm.AgentStatuses["The Offensive Line #2"].Status);
        Assert.Equal("Pending", vm.AgentStatuses["The Safety"].Status);
    }

    [Fact]
    public async Task Replaying_a_drive_labels_its_plays_the_way_the_orchestrator_announced_them()
    {
        var (vm, _) = await LoadedAsync(StoredDrive());

        Assert.Equal(["The Offensive Line #1", "The Offensive Line #2", "The Safety"], vm.Plays.Select(p => p.AgentName));
        Assert.Single(vm.Logs);
        Assert.Single(vm.Turns);
    }

    [Fact]
    public async Task A_status_update_for_a_new_instance_adds_a_card_rather_than_dropping_it()
    {
        var (vm, _) = await LoadedAsync(StoredDrive());

        vm.UpdateAgentStatus("The Offensive Line #3", "Running", 0);
        vm.UpdateAgentStatus("The Safety", "Completed", 100);

        Assert.Equal("Running", vm.AgentStatuses["The Offensive Line #3"].Status);
        Assert.Equal(100, vm.AgentStatuses["The Safety"].Progress);
    }

    [Fact]
    public async Task A_play_status_update_changes_only_that_play()
    {
        var (vm, _) = await LoadedAsync(StoredDrive());
        var started = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        vm.UpdatePlayStatus(3, "Completed", started, started.AddMinutes(2));

        var review = vm.Plays.Single(p => p.Id == 3);
        Assert.Equal(PlayStatus.Completed, review.Status);
        Assert.Equal(TimeSpan.FromMinutes(2), review.Duration);
        Assert.Equal(PlayStatus.InProgress, vm.Plays.Single(p => p.Id == 2).Status);
    }

    [Theory]
    [InlineData("Touchdown", DriveStatus.Touchdown)]
    [InlineData("Turnover", DriveStatus.Turnover)]
    [InlineData("Cancelled", DriveStatus.Turnover)]
    public async Task A_completed_drive_takes_the_status_the_hub_sent(string wire, DriveStatus expected)
    {
        // Anything that is not a Touchdown is shown as a Turnover, which is why the hub has
        // to deliver the word "Touchdown" intact (AgentHubWireContractTests).
        var (vm, _) = await LoadedAsync(StoredDrive());

        vm.MarkDriveCompleted(wire);

        Assert.Equal(expected, vm.Drive!.Status);
        Assert.NotNull(vm.Drive.CompletedAt);
    }

    [Fact]
    public async Task An_unknown_drive_leaves_the_board_empty_without_failing()
    {
        var (vm, _) = await LoadedAsync(null);

        Assert.Null(vm.Drive);
        Assert.Empty(vm.AgentStatuses);
        Assert.Empty(vm.Plays);
    }

    [Fact]
    public async Task A_cancel_that_fails_is_reported_as_a_page_error_and_the_drive_stays_shown()
    {
        var (vm, service) = await LoadedAsync(StoredDrive());
        service.CancelFailure = new DrivesMonitorServiceException("orchestrator unavailable");

        var ex = await Assert.ThrowsAsync<DrivesMonitorPageVMException>(() => vm.CancelDrive());

        Assert.Contains("orchestrator unavailable", ex.InnerException?.Message);
        Assert.NotNull(vm.Drive);
    }

    [Fact]
    public async Task Renaming_trims_the_name_and_clears_it_when_blank()
    {
        var (vm, service) = await LoadedAsync(StoredDrive());

        await vm.RenameDrive("  Login drive  ");
        Assert.Equal("Login drive", vm.Drive!.Name);
        Assert.Equal("  Login drive  ", service.RenamedTo);

        await vm.RenameDrive("   ");
        Assert.Null(vm.Drive.Name);
    }

    private sealed class ScriptedMonitorService : IDrivesMonitorService
    {
        public Drive? Drive { get; init; }
        public Exception? CancelFailure { get; set; }
        public string? RenamedTo { get; private set; }

        public Task<Drive?> GetDriveAsync(string driveId) => Task.FromResult(Drive);

        public Task CancelDriveAsync(string driveId) =>
            CancelFailure is null ? Task.CompletedTask : throw CancelFailure;

        public Task RenameDriveAsync(string driveId, string? name)
        {
            RenamedTo = name;
            return Task.CompletedTask;
        }
    }
}
