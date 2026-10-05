using Microsoft.EntityFrameworkCore;
using MudBlazor;
using TD.Areas.Teams.Index;
using TD.Models;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.ViewModels;

/// <summary>
/// The Teams page view model over the real service and data access. What matters is what
/// the user is told and what the roster on screen still shows after a refusal or a failure:
/// a blocked save keeps the editor open, a failed one keeps the edits, and a refusal is
/// shown as guidance rather than a crash.
/// </summary>
public class TeamsIndexPageVMTests
{
    private static (TeamsIndexPageVM VM, FakeSnackbar Snackbar) Create(TestDb db)
    {
        var snackbar = new FakeSnackbar();
        return (new TeamsIndexPageVM(new TeamsIndexService(new TeamsIndexServiceDA(db)), snackbar), snackbar);
    }

    private static async Task<(TeamsIndexPageVM VM, FakeSnackbar Snackbar, AgentTeam Playbook)> LoadedAsync(TestDb db)
    {
        var (vm, snackbar) = Create(db);
        await vm.Loaded();
        return (vm, snackbar, vm.Teams.Single(t => t.IsDefault));
    }

    private static async Task<string?> StoredPromptAsync(TestDb db, int memberId)
    {
        await using var ctx = db.CreateDbContext();
        return (await ctx.AgentMembers.SingleAsync(m => m.Id == memberId)).SystemPrompt;
    }

    [Fact]
    public async Task Saving_an_empty_prompt_is_blocked_and_the_editor_stays_open()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, playbook) = await LoadedAsync(db);
        var member = playbook.GetLeader()!;
        var before = member.SystemPrompt;
        vm.BeginEdit(member);
        vm.EditBuffer = "   ";

        await vm.SaveEdit(member);

        var shown = Assert.Single(snackbar.Shown);
        Assert.Equal(Severity.Warning, shown.Severity);
        Assert.Contains("empty", shown.Message);
        Assert.Equal(member.Id, vm.EditingMemberId);
        Assert.Equal(before, await StoredPromptAsync(db, member.Id));
    }

    [Fact]
    public async Task A_saved_prompt_is_trimmed_persisted_and_the_editor_closes()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, playbook) = await LoadedAsync(db);
        var member = playbook.GetLeader()!;
        vm.BeginEdit(member);
        vm.EditBuffer = "  You call the plays.  ";

        await vm.SaveEdit(member);

        Assert.Equal("You call the plays.", await StoredPromptAsync(db, member.Id));
        Assert.Equal("You call the plays.", member.SystemPrompt);
        Assert.Null(vm.EditingMemberId);
        Assert.Equal("", vm.EditBuffer);
        Assert.False(vm.IsSaving);
        Assert.Equal(Severity.Success, Assert.Single(snackbar.Shown).Severity);
    }

    [Fact]
    public async Task A_save_that_fails_keeps_the_editor_and_the_edits_and_reports_the_failure()
    {
        var snackbar = new FakeSnackbar();
        var vm = new TeamsIndexPageVM(new FailingTeamsService(), snackbar);
        var member = new AgentMember { Id = 7, Name = "QB", SystemPrompt = "old prompt" };
        vm.BeginEdit(member);
        vm.EditBuffer = "new prompt";

        await vm.SaveEdit(member);

        Assert.Equal("old prompt", member.SystemPrompt);
        Assert.Equal(7, vm.EditingMemberId);
        Assert.Equal("new prompt", vm.EditBuffer);
        Assert.False(vm.IsSaving);
        var shown = Assert.Single(snackbar.Shown);
        Assert.Equal(Severity.Error, shown.Severity);
        Assert.Contains("database is down", shown.Message);
    }

    [Fact]
    public async Task A_failed_effort_change_leaves_the_roster_as_it_was()
    {
        var snackbar = new FakeSnackbar();
        var vm = new TeamsIndexPageVM(new FailingTeamsService(), snackbar);
        var member = new AgentMember { Id = 7, Name = "QB", Effort = AgentEffort.High };

        await vm.SaveMemberEffort(member, AgentEffort.Low);

        Assert.Equal(AgentEffort.High, member.Effort);
        Assert.Equal(Severity.Error, Assert.Single(snackbar.Shown).Severity);
    }

    [Fact]
    public async Task Creating_a_team_without_a_name_is_refused_and_nothing_is_added()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, _) = await LoadedAsync(db);
        var count = vm.Teams.Count;

        await vm.CreateTeam("   ", null);

        var shown = Assert.Single(snackbar.Shown);
        Assert.Equal(Severity.Error, shown.Severity);
        Assert.Contains("name", shown.Message);
        Assert.Equal(count, vm.Teams.Count);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task Creating_a_team_adds_it_to_the_list_with_empty_collections()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, _) = await LoadedAsync(db);

        await vm.CreateTeam("Special Squad", "for special work");

        var team = Assert.Single(vm.Teams, t => t.Name == "Special Squad");
        Assert.Empty(team.Members);
        Assert.Equal(Severity.Success, Assert.Single(snackbar.Shown).Severity);
    }

    [Fact]
    public async Task Deleting_the_default_team_is_refused_with_guidance_and_the_team_stays_listed()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, playbook) = await LoadedAsync(db);

        await vm.DeleteTeam(playbook);

        var shown = Assert.Single(snackbar.Shown);
        Assert.Equal(Severity.Warning, shown.Severity);
        Assert.Contains("default", shown.Message);
        Assert.Contains(vm.Teams, t => t.Id == playbook.Id);
    }

    [Fact]
    public async Task Removing_the_only_leader_is_refused_and_the_member_stays_on_the_roster()
    {
        using var db = TestDb.Create();
        var (vm, snackbar, playbook) = await LoadedAsync(db);
        var leader = playbook.GetLeader()!;

        await vm.RemoveMember(playbook, leader);

        var shown = Assert.Single(snackbar.Shown);
        Assert.Equal(Severity.Warning, shown.Severity);
        Assert.Contains("leader", shown.Message);
        Assert.Contains(playbook.Members, m => m.Id == leader.Id);
    }

    [Fact]
    public async Task Removing_a_worker_drops_it_from_the_roster_and_the_database()
    {
        using var db = TestDb.Create();
        var (vm, _, playbook) = await LoadedAsync(db);
        var worker = playbook.Members.First(m => m.Role == AgentRole.Worker);

        await vm.RemoveMember(playbook, worker);

        Assert.DoesNotContain(playbook.Members, m => m.Id == worker.Id);
        await using var ctx = db.CreateDbContext();
        Assert.Null(await ctx.AgentMembers.FindAsync(worker.Id));
    }

    [Fact]
    public async Task A_page_that_cannot_load_surfaces_the_failure_to_the_page()
    {
        var vm = new TeamsIndexPageVM(new FailingTeamsService(), new FakeSnackbar());

        await Assert.ThrowsAsync<TeamsIndexPageVMException>(() => vm.Loaded());

        Assert.Empty(vm.Teams);
    }

    /// <summary>A service whose every call fails the way a dead database would.</summary>
    private sealed class FailingTeamsService : ITeamsIndexService
    {
        private static TeamsIndexServiceException Down() => new("the database is down");

        public Task<List<AgentTeam>> GetAllTeamsAsync() => throw Down();
        public Task UpdateMemberPromptAsync(int memberId, string systemPrompt) => throw Down();
        public Task UpdateMemberEffortAsync(int memberId, AgentEffort effort) => throw Down();
        public Task UpdateMemberModelAsync(int memberId, ClaudeModel model) => throw Down();
        public Task UpdateMemberMaxInstancesAsync(int memberId, int maxInstances) => throw Down();
        public Task<AgentTeam> CreateTeamAsync(string name, string? description) => throw Down();
        public Task RenameTeamAsync(int teamId, string name, string? description) => throw Down();
        public Task DeleteTeamAsync(int teamId) => throw Down();
        public Task SetDefaultTeamAsync(int teamId) => throw Down();
        public Task<AgentMember> AddMemberAsync(int teamId, AgentMember member) => throw Down();
        public Task RemoveMemberAsync(int memberId) => throw Down();
        public Task RenameMemberAsync(int memberId, string name) => throw Down();
    }
}
