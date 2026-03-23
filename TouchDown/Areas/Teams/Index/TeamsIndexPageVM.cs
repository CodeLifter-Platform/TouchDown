using CommunityToolkit.Mvvm.ComponentModel;
using MudBlazor;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexPageVM
{
    List<AgentTeam> Teams { get; }
    Task Loaded();
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
    private readonly Serilog.ILogger _log = Log.ForContext<TeamsIndexPageVM>();

    public TeamsIndexPageVM(ITeamsIndexService service)
    {
        _service = service;
    }

    [ObservableProperty]
    private List<AgentTeam> _teams = [];

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
