using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Teams.Index;

public interface ITeamsIndexServiceDA
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

    /// <summary>Drives referencing this team. A team with drives cannot be deleted without orphaning them.</summary>
    Task<int> CountDrivesForTeamAsync(int teamId);
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

    public async Task UpdateMemberPromptAsync(int memberId, string systemPrompt)
    {
        _log.Debug("Updating system prompt for member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");
            member.SystemPrompt = systemPrompt;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to update system prompt for member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to update system prompt for member {memberId}", ex);
        }
    }

    public async Task UpdateMemberEffortAsync(int memberId, AgentEffort effort)
    {
        _log.Debug("Updating effort for member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");
            member.Effort = effort;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to update effort for member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to update effort for member {memberId}", ex);
        }
    }

    public async Task UpdateMemberModelAsync(int memberId, ClaudeModel model)
    {
        _log.Debug("Updating model for member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");
            member.Model = model;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to update model for member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to update model for member {memberId}", ex);
        }
    }

    public async Task UpdateMemberMaxInstancesAsync(int memberId, int maxInstances)
    {
        _log.Debug("Updating fan-out for member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");
            member.MaxInstances = maxInstances;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to update fan-out for member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to update fan-out for member {memberId}", ex);
        }
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    public async Task<AgentTeam> CreateTeamAsync(string name, string? description)
    {
        _log.Information("Creating team {TeamName}", name);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var team = new AgentTeam { Name = name, Description = description, IsDefault = false };
            db.AgentTeams.Add(team);
            await db.SaveChangesAsync();
            return team;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to create team {TeamName}", name);
            throw new TeamsIndexServiceDAException($"Failed to create team '{name}'", ex);
        }
    }

    public async Task RenameTeamAsync(int teamId, string name, string? description)
    {
        _log.Debug("Renaming team {TeamId}", teamId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var team = await db.AgentTeams.FindAsync(teamId)
                ?? throw new TeamsIndexServiceDAException($"Team {teamId} not found");
            team.Name = name;
            team.Description = description;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to rename team {TeamId}", teamId);
            throw new TeamsIndexServiceDAException($"Failed to rename team {teamId}", ex);
        }
    }

    public async Task DeleteTeamAsync(int teamId)
    {
        _log.Information("Deleting team {TeamId}", teamId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var team = await db.AgentTeams.FindAsync(teamId)
                ?? throw new TeamsIndexServiceDAException($"Team {teamId} not found");

            // Drives hold a required AgentTeamId; deleting a referenced team would orphan
            // their history and break the monitor page for every past drive.
            var driveCount = await db.Drives.CountAsync(d => d.AgentTeamId == teamId);
            if (driveCount > 0)
                throw new TeamsIndexServiceDAException(
                    $"'{team.Name}' is used by {driveCount} drive(s) and cannot be deleted.");

            if (team.IsDefault)
                throw new TeamsIndexServiceDAException(
                    $"'{team.Name}' is the default team. Make another team the default first.");

            db.AgentTeams.Remove(team);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to delete team {TeamId}", teamId);
            throw new TeamsIndexServiceDAException($"Failed to delete team {teamId}", ex);
        }
    }

    public async Task SetDefaultTeamAsync(int teamId)
    {
        _log.Information("Setting team {TeamId} as default", teamId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var team = await db.AgentTeams.FindAsync(teamId)
                ?? throw new TeamsIndexServiceDAException($"Team {teamId} not found");

            // Exactly one default at a time.
            await db.AgentTeams.Where(t => t.IsDefault)
                .ForEachAsync(t => t.IsDefault = false);
            team.IsDefault = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to set default team {TeamId}", teamId);
            throw new TeamsIndexServiceDAException($"Failed to set default team {teamId}", ex);
        }
    }

    public async Task<int> CountDrivesForTeamAsync(int teamId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Drives.CountAsync(d => d.AgentTeamId == teamId);
    }

    // ── Members ──────────────────────────────────────────────────────────────

    public async Task<AgentMember> AddMemberAsync(int teamId, AgentMember member)
    {
        _log.Information("Adding member {MemberName} to team {TeamId}", member.Name, teamId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            _ = await db.AgentTeams.FindAsync(teamId)
                ?? throw new TeamsIndexServiceDAException($"Team {teamId} not found");

            member.AgentTeamId = teamId;
            member.AgentTeam = null;
            db.AgentMembers.Add(member);
            await db.SaveChangesAsync();
            return member;
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to add member to team {TeamId}", teamId);
            throw new TeamsIndexServiceDAException($"Failed to add member to team {teamId}", ex);
        }
    }

    public async Task RemoveMemberAsync(int memberId)
    {
        _log.Information("Removing member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");

            // A team without a leader cannot plan a drive — the Quarterback is what runs
            // the huddle and produces the plan.
            if (member.Role == AgentRole.Leader)
            {
                var otherLeaders = await db.AgentMembers.CountAsync(m =>
                    m.AgentTeamId == member.AgentTeamId && m.Role == AgentRole.Leader && m.Id != memberId);
                if (otherLeaders == 0)
                    throw new TeamsIndexServiceDAException(
                        "A team needs a leader. Add another leader before removing this one.");
            }

            db.AgentMembers.Remove(member);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to remove member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to remove member {memberId}", ex);
        }
    }

    public async Task RenameMemberAsync(int memberId, string name)
    {
        _log.Debug("Renaming member {MemberId}", memberId);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var member = await db.AgentMembers.FindAsync(memberId)
                ?? throw new TeamsIndexServiceDAException($"Agent member {memberId} not found");
            member.Name = name;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is not TeamsIndexServiceDAException)
        {
            _log.Error(ex, "Failed to rename member {MemberId}", memberId);
            throw new TeamsIndexServiceDAException($"Failed to rename member {memberId}", ex);
        }
    }
}
