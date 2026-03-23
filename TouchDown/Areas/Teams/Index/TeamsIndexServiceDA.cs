using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexServiceDA
{
    Task<List<AgentTeam>> GetAllTeamsAsync();
}

public class TeamsIndexServiceDAException : Exception
{
    public TeamsIndexServiceDAException() { }
    public TeamsIndexServiceDAException(string message) : base(message) { }
    public TeamsIndexServiceDAException(string message, Exception innerException) : base(message, innerException) { }
}

public class TeamsIndexServiceDA : ITeamsIndexServiceDA
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly Serilog.ILogger _log = Log.ForContext<TeamsIndexServiceDA>();

    public TeamsIndexServiceDA(IDbContextFactory<TDDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<AgentTeam>> GetAllTeamsAsync()
    {
        _log.Debug("Fetching all agent teams");
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
            _log.Error(ex, "Failed to fetch agent teams");
            throw new TeamsIndexServiceDAException("Failed to fetch agent teams", ex);
        }
    }
}
