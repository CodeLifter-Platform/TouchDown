using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Drives.New;

public interface IDrivesNewServiceDA
{
    Task<List<AgentTeam>> GetAvailableTeamsAsync();
    Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team);
}

public class DrivesNewServiceDAException : Exception
{
    public DrivesNewServiceDAException() { }
    public DrivesNewServiceDAException(string message) : base(message) { }
    public DrivesNewServiceDAException(string message, Exception innerException) : base(message, innerException) { }
}

public class DrivesNewServiceDA : IDrivesNewServiceDA
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesNewServiceDA>();

    public DrivesNewServiceDA(IDbContextFactory<TDDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<AgentTeam>> GetAvailableTeamsAsync()
    {
        _log.Debug("Fetching available teams for drive setup");
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AgentTeams
                .Include(t => t.Members)
                .Include(t => t.CommunicationRules)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to fetch available teams");
            throw new DrivesNewServiceDAException("Failed to fetch available teams", ex);
        }
    }

    public async Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team)
    {
        _log.Debug("Saving custom team {TeamName}", team.Name);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.AgentTeams.Add(team);
            await db.SaveChangesAsync();
            return team;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save custom team {TeamName}", team.Name);
            throw new DrivesNewServiceDAException($"Failed to save custom team '{team.Name}'", ex);
        }
    }
}
