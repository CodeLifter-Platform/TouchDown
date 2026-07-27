using Serilog;
using TD.Models;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexService
{
    Task<List<AgentTeam>> GetAllTeamsAsync();
    Task UpdateMemberPromptAsync(int memberId, string systemPrompt);
    Task UpdateMemberEffortAsync(int memberId, AgentEffort effort);
    Task UpdateMemberModelAsync(int memberId, ClaudeModel model);
    Task UpdateMemberMaxInstancesAsync(int memberId, int maxInstances);

    Task<AgentTeam> CreateTeamAsync(string name, string? description);
    Task RenameTeamAsync(int teamId, string name, string? description);
    Task DeleteTeamAsync(int teamId);
    Task SetDefaultTeamAsync(int teamId);

    Task<AgentMember> AddMemberAsync(int teamId, AgentMember member);
    Task RemoveMemberAsync(int memberId);
    Task RenameMemberAsync(int memberId, string name);
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

    public async Task UpdateMemberMaxInstancesAsync(int memberId, int maxInstances)
    {
        if (maxInstances < 1)
            throw new TeamsIndexServiceException("An agent must allow at least one instance.");

        try
        {
            await _da.UpdateMemberMaxInstancesAsync(memberId, maxInstances);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to update fan-out for member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to update fan-out for member {memberId}", ex);
        }
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    public async Task<AgentTeam> CreateTeamAsync(string name, string? description)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new TeamsIndexServiceException("A team needs a name.");

        try
        {
            return await _da.CreateTeamAsync(trimmed, description?.Trim());
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to create team {TeamName}", name);
            throw new TeamsIndexServiceException($"Failed to create team '{name}'", ex);
        }
    }

    public async Task RenameTeamAsync(int teamId, string name, string? description)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new TeamsIndexServiceException("A team needs a name.");

        try
        {
            await _da.RenameTeamAsync(teamId, trimmed, description?.Trim());
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to rename team {TeamId}", teamId);
            throw new TeamsIndexServiceException($"Failed to rename team {teamId}", ex);
        }
    }

    public async Task DeleteTeamAsync(int teamId)
    {
        try
        {
            await _da.DeleteTeamAsync(teamId);
        }
        catch (TeamsIndexServiceDAException ex)
        {
            // The DA's guard messages (in use by drives / is the default) are written for
            // the user, so surface them rather than replacing them with a generic failure.
            throw new TeamsIndexServiceException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to delete team {TeamId}", teamId);
            throw new TeamsIndexServiceException($"Failed to delete team {teamId}", ex);
        }
    }

    public async Task SetDefaultTeamAsync(int teamId)
    {
        try
        {
            await _da.SetDefaultTeamAsync(teamId);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to set default team {TeamId}", teamId);
            throw new TeamsIndexServiceException($"Failed to set default team {teamId}", ex);
        }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    public async Task<AgentMember> AddMemberAsync(int teamId, AgentMember member)
    {
        if (string.IsNullOrWhiteSpace(member.Name))
            throw new TeamsIndexServiceException("An agent needs a name.");

        member.Name = member.Name.Trim();
        if (member.MaxInstances < 1) member.MaxInstances = 1;

        try
        {
            return await _da.AddMemberAsync(teamId, member);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to add member to team {TeamId}", teamId);
            throw new TeamsIndexServiceException($"Failed to add member to team {teamId}", ex);
        }
    }

    public async Task RemoveMemberAsync(int memberId)
    {
        try
        {
            await _da.RemoveMemberAsync(memberId);
        }
        catch (TeamsIndexServiceDAException ex)
        {
            // Preserve the "a team needs a leader" guard message.
            throw new TeamsIndexServiceException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to remove member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to remove member {memberId}", ex);
        }
    }

    public async Task RenameMemberAsync(int memberId, string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new TeamsIndexServiceException("An agent needs a name.");

        try
        {
            await _da.RenameMemberAsync(memberId, trimmed);
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceException)
        {
            _log.Error(ex, "Failed to rename member {MemberId}", memberId);
            throw new TeamsIndexServiceException($"Failed to rename member {memberId}", ex);
        }
    }
}
