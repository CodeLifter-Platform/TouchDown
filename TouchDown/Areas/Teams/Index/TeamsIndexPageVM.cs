using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudBlazor;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexPageVM
{
    List<AgentTeam> Teams { get; }
    int? EditingMemberId { get; }
    string EditBuffer { get; set; }
    bool IsSaving { get; }
    Task Loaded();
    void BeginEdit(AgentMember member);
    void CancelEdit();
    Task SaveEdit(AgentMember member);
    Task SaveMemberEffort(AgentMember member, AgentEffort effort);
    Task SaveMemberModel(AgentMember member, ClaudeModel model);
    Task SaveMemberMaxInstances(AgentMember member, int maxInstances);

    Task CreateTeam(string name, string? description);
    Task RenameTeam(AgentTeam team, string name, string? description);
    Task DeleteTeam(AgentTeam team);
    Task SetDefaultTeam(AgentTeam team);

    Task AddMember(AgentTeam team, AgentMember member);
    Task RemoveMember(AgentTeam team, AgentMember member);
    Task RenameMember(AgentMember member, string name);
}

public class TeamsIndexPageVMException : Exception
{
    public TeamsIndexPageVMException() { }
    public TeamsIndexPageVMException(string message) : base(message) { }
    public TeamsIndexPageVMException(string message, Exception innerException) : base(message, innerException) { }
}

public partial class TeamsIndexPageVM : VM, ITeamsIndexPageVM
{
    private readonly ITeamsIndexService _service;
    private readonly ISnackbar _snackbar;
    private readonly Serilog.ILogger _log = Log.ForContext<TeamsIndexPageVM>();

    public TeamsIndexPageVM(ITeamsIndexService service, ISnackbar snackbar)
    {
        _service = service;
        _snackbar = snackbar;
    }

    [ObservableProperty]
    private List<AgentTeam> _teams = [];

    /// <summary>Id of the member whose prompt is currently being edited, or null.</summary>
    [ObservableProperty]
    private int? _editingMemberId;

    /// <summary>Working copy of the prompt while editing.</summary>
    [ObservableProperty]
    private string _editBuffer = "";

    [ObservableProperty]
    private bool _isSaving;

    public override async Task Loaded()
    {
        _log.Debug("Loading Teams Index page");
        try
        {
            Teams = await _service.GetAllTeamsAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load Teams Index page");
            throw new TeamsIndexPageVMException("Failed to load Teams Index page", ex);
        }
    }

    public void BeginEdit(AgentMember member)
    {
        EditingMemberId = member.Id;
        EditBuffer = member.SystemPrompt ?? "";
    }

    public void CancelEdit()
    {
        EditingMemberId = null;
        EditBuffer = "";
    }

    [RelayCommand]
    public async Task SaveEdit(AgentMember member)
    {
        var newPrompt = EditBuffer.Trim();
        if (string.IsNullOrWhiteSpace(newPrompt))
        {
            _snackbar.Add("System prompt can't be empty.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await _service.UpdateMemberPromptAsync(member.Id, newPrompt);
            member.SystemPrompt = newPrompt; // reflect in the loaded roster
            EditingMemberId = null;
            EditBuffer = "";
            _snackbar.Add($"Updated {member.Name}'s prompt.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save prompt for member {MemberId}", member.Id);
            _snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task SaveMemberEffort(AgentMember member, AgentEffort effort)
    {
        if (member.Effort == effort) return;
        try
        {
            await _service.UpdateMemberEffortAsync(member.Id, effort);
            member.Effort = effort; // reflect in the loaded roster
            _snackbar.Add($"Set {member.Name}'s effort to {effort.ToDisplayName()}.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save effort for member {MemberId}", member.Id);
            _snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
        }
    }

    public async Task SaveMemberModel(AgentMember member, ClaudeModel model)
    {
        if (member.Model == model) return;
        try
        {
            await _service.UpdateMemberModelAsync(member.Id, model);
            member.Model = model; // reflect in the loaded roster
            _snackbar.Add($"Set {member.Name}'s model to {model.ToDisplayName()}.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save model for member {MemberId}", member.Id);
            _snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
        }
    }

    public async Task SaveMemberMaxInstances(AgentMember member, int maxInstances)
    {
        if (member.MaxInstances == maxInstances) return;
        try
        {
            await _service.UpdateMemberMaxInstancesAsync(member.Id, maxInstances);
            member.MaxInstances = maxInstances;
            _snackbar.Add(
                maxInstances > 1
                    ? $"{member.Name} can now fan out into up to {maxInstances} instances."
                    : $"{member.Name} now runs as a single instance.",
                Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save fan-out for member {MemberId}", member.Id);
            _snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
        }
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    public async Task CreateTeam(string name, string? description)
    {
        IsSaving = true;
        try
        {
            var team = await _service.CreateTeamAsync(name, description);
            // Reload so the new team arrives with its (empty) navigation collections.
            Teams = await _service.GetAllTeamsAsync();
            _snackbar.Add($"Created {team.Name}.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to create team {TeamName}", name);
            _snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task RenameTeam(AgentTeam team, string name, string? description)
    {
        try
        {
            await _service.RenameTeamAsync(team.Id, name, description);
            team.Name = name.Trim();
            team.Description = description?.Trim();
            NotifyStateChanged();
            _snackbar.Add("Team updated.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to rename team {TeamId}", team.Id);
            _snackbar.Add(ex.Message, Severity.Error);
        }
    }

    public async Task DeleteTeam(AgentTeam team)
    {
        try
        {
            await _service.DeleteTeamAsync(team.Id);
            Teams = Teams.Where(t => t.Id != team.Id).ToList();
            _snackbar.Add($"Deleted {team.Name}.", Severity.Success);
        }
        catch (Exception ex)
        {
            // Refusals here are expected (team in use by drives, or is the default) and the
            // message explains what to do, so show it rather than a generic failure.
            _log.Warning(ex, "Could not delete team {TeamId}", team.Id);
            _snackbar.Add(ex.Message, Severity.Warning);
        }
    }

    public async Task SetDefaultTeam(AgentTeam team)
    {
        try
        {
            await _service.SetDefaultTeamAsync(team.Id);
            foreach (var t in Teams) t.IsDefault = t.Id == team.Id;
            NotifyStateChanged();
            _snackbar.Add($"{team.Name} is now the default team.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to set default team {TeamId}", team.Id);
            _snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    public async Task AddMember(AgentTeam team, AgentMember member)
    {
        try
        {
            var saved = await _service.AddMemberAsync(team.Id, member);
            team.Members.Add(saved);
            NotifyStateChanged();
            _snackbar.Add($"Added {saved.Name} to {team.Name}.", Severity.Success);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to add member to team {TeamId}", team.Id);
            _snackbar.Add(ex.Message, Severity.Error);
        }
    }

    public async Task RemoveMember(AgentTeam team, AgentMember member)
    {
        try
        {
            await _service.RemoveMemberAsync(member.Id);
            team.Members.Remove(member);
            NotifyStateChanged();
            _snackbar.Add($"Removed {member.Name}.", Severity.Success);
        }
        catch (Exception ex)
        {
            // "A team needs a leader" is a guard, not a bug — surface it as guidance.
            _log.Warning(ex, "Could not remove member {MemberId}", member.Id);
            _snackbar.Add(ex.Message, Severity.Warning);
        }
    }

    public async Task RenameMember(AgentMember member, string name)
    {
        try
        {
            await _service.RenameMemberAsync(member.Id, name);
            member.Name = name.Trim();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to rename member {MemberId}", member.Id);
            _snackbar.Add(ex.Message, Severity.Error);
        }
    }

    public static Color GetRoleColor(AgentRole role) => role switch
    {
        AgentRole.Leader => Color.Primary,
        AgentRole.Worker => Color.Info,
        AgentRole.Validator => Color.Success,
        AgentRole.Tester => Color.Secondary,
        AgentRole.DevOps => Color.Warning,
        AgentRole.Researcher => Color.Tertiary,
        _ => Color.Default
    };
}
