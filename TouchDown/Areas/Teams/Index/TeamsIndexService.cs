using Serilog;
using TD.Models;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexService
{
    Task<List<AgentTeam>> GetAllTeamsAsync();
}

public class TeamsIndexServiceException : Exception
{
    public TeamsIndexServiceException() { }
    public TeamsIndexServiceException(string message) : base(message) { }
    public TeamsIndexServiceException(string message, Exception innerException) : base(message, innerException) { }
}

public class TeamsIndexService : ITeamsIndexService
{
    private readonly ITeamsIndexServiceDA _da;
    private readonly Serilog.ILogger _log = Log.ForContext<TeamsIndexService>();

    public TeamsIndexService(ITeamsIndexServiceDA da)
    {
        _da = da;
    }

    public async Task<List<AgentTeam>> GetAllTeamsAsync()
    {
        _log.Debug("Getting all teams");
        try
        {
            return await _da.GetAllTeamsAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to get all teams");
            throw new TeamsIndexServiceException("Failed to get all teams", ex);
        }
    }
}
