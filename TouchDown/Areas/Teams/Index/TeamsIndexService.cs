using Serilog;
using TD.Models;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexService
{
    Task<List<AgentTeam>> GetAllTeamsAsync();
    Task UpdateMemberPromptAsync(int memberId, string systemPrompt);
    Task UpdateMemberEffortAsync(int memberId, AgentEffort effort);
    Task UpdateMemberModelAsync(int memberId, ClaudeModel model);
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

    public async Task UpdateMemberPromptAsync(int memberId, string systemPrompt)
    {
        _log.Debug("Updating system prompt for member {MemberId}", memberId);
        try
        {
            await _da.UpdateMemberPromptAsync(memberId, systemPrompt);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to update system prompt for member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to update system prompt for member {memberId}", ex);
        }
    }

    public async Task UpdateMemberEffortAsync(int memberId, AgentEffort effort)
    {
        _log.Debug("Updating effort for member {MemberId}", memberId);
        try
        {
            await _da.UpdateMemberEffortAsync(memberId, effort);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to update effort for member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to update effort for member {memberId}", ex);
        }
    }

    public async Task UpdateMemberModelAsync(int memberId, ClaudeModel model)
    {
        _log.Debug("Updating model for member {MemberId}", memberId);
        try
        {
            await _da.UpdateMemberModelAsync(memberId, model);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to update model for member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to update model for member {memberId}", ex);
        }
    }
}
