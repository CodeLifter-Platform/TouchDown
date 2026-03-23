using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using TD.Data;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.ViewModels;

public partial class TeamsVM : VM
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;

    public TeamsVM(IDbContextFactory<TDDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [ObservableProperty]
    private List<AgentTeam> _teams = [];

    public override async Task Loaded()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Teams = await db.AgentTeams
            .Include(t => t.Members)
            .Include(t => t.CommunicationRules)
            .ToListAsync();
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
