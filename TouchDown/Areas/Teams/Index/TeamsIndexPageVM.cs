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

    public static Color GetRoleColor(AgentRole role) => role switch
    {
        AgentRole.Leader => Color.Primary,
        AgentRole.Worker => Color.Info,
        AgentRole.Validator => Color.Success,
        AgentRole.Tester => Color.Secondary,
        AgentRole.DevOps => Color.Warning,
        _ => Color.Default
    };
}
