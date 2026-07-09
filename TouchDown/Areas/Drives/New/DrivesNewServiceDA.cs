using Microsoft.EntityFrameworkCore;
using Serilog;
using TD.Data;
using TD.Models;

namespace TD.Areas.Drives.New;

public interface IDrivesNewServiceDA
{
    Task<List<AgentTeam>> GetAvailableTeamsAsync();
    Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team);

    /// <summary>Persists a draft Drive (Status=Huddle) when the Huddle opens, so turns can attach as they happen.</summary>
    Task<Drive> CreateDraftDriveAsync(AgentSession session);

    /// <summary>Appends a single conversation turn to a Drive.</summary>
    Task AddTurnAsync(DriveTurn turn);
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

    public async Task<Drive> CreateDraftDriveAsync(AgentSession session)
    {
        _log.Debug("Creating draft drive for huddle");
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var drive = new Drive
            {
                Name = string.IsNullOrWhiteSpace(session.Name) ? null : session.Name.Trim(),
                TaskDescription = session.TaskDescription ?? string.Empty,
                MaxParallelism = session.MaxParallelism,
                SourceType = session.SourceType,
                RepoPath = session.RepoPath,
                Branch = session.Branch,
                WorkspaceMode = session.WorkspaceMode,
                PrBranchName = session.PrBranchName,
                AgentTeamId = session.Team.Id,
                ProviderId = session.ProviderId,
                ModelId = session.ModelId,
                Effort = session.Effort,
                OverrideTeamConfig = session.OverrideTeamConfig,
                Status = DriveStatus.Huddle
            };
            db.Drives.Add(drive);
            await db.SaveChangesAsync();
            return drive;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to create draft drive");
            throw new DrivesNewServiceDAException("Failed to create draft drive", ex);
        }
    }

    public async Task AddTurnAsync(DriveTurn turn)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.DriveTurns.Add(turn);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // A failed turn write must not break the live conversation — log and move on.
            _log.Warning(ex, "Failed to persist drive turn for drive {DriveId}", turn.DriveId);
        }
    }
}
