using Serilog;
using TD.Models;
using TD.Services;

namespace TD.Areas.Drives.New;

public record AvailableProvider(string ProviderId, string DisplayName, IReadOnlyList<AgentModel> Models);

public interface IDrivesNewService
{
    Task<List<AvailableProvider>> GetAvailableProvidersAsync();
    Task<List<AgentTeam>> GetAvailableTeamsAsync();
    Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team);
    Task<Drive> StartDriveAsync(AgentSession session);
    Task<Drive> CreateDraftDriveAsync(AgentSession session);
    Task AddTurnAsync(DriveTurn turn);
    IAsyncEnumerable<string> StreamQbResponseAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamCoordinatorResearchAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default);
}

public class DrivesNewServiceException : Exception
{
    public DrivesNewServiceException() { }
    public DrivesNewServiceException(string message) : base(message) { }
    public DrivesNewServiceException(string message, Exception innerException) : base(message, innerException) { }
}

public class DrivesNewService : IDrivesNewService
{
    private readonly IDrivesNewServiceDA _da;
    private readonly IAgentOrchestrationService _orchestration;
    private readonly IClaudeStreamingService _claude;
    private readonly IAgentProviderRegistry _providerRegistry;
    private readonly Serilog.ILogger _log = Log.ForContext<DrivesNewService>();

    public DrivesNewService(IDrivesNewServiceDA da, IAgentOrchestrationService orchestration, IClaudeStreamingService claude, IAgentProviderRegistry providerRegistry)
    {
        _da = da;
        _orchestration = orchestration;
        _claude = claude;
        _providerRegistry = providerRegistry;
    }

    public async Task<List<AvailableProvider>> GetAvailableProvidersAsync()
    {
        _log.Debug("Getting available providers");
        var available = await _providerRegistry.GetAvailableAsync();
        return available.Select(p => new AvailableProvider(p.ProviderId, p.DisplayName, p.AvailableModels)).ToList();
    }

    public async Task<List<AgentTeam>> GetAvailableTeamsAsync()
    {
        _log.Debug("Getting available teams");
        try
        {
            return await _da.GetAvailableTeamsAsync();
        }
        catch (Exception ex) when (ex is not DrivesNewServiceException)
        {
            _log.Error(ex, "Failed to get available teams");
            throw new DrivesNewServiceException("Failed to get available teams", ex);
        }
    }

    public async Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team)
    {
        _log.Debug("Saving custom team {TeamName}", team.Name);
        try
        {
            return await _da.SaveCustomTeamAsync(team);
        }
        catch (Exception ex) when (ex is not DrivesNewServiceException)
        {
            _log.Error(ex, "Failed to save custom team");
            throw new DrivesNewServiceException("Failed to save custom team", ex);
        }
    }

    public async Task<Drive> StartDriveAsync(AgentSession session)
    {
        _log.Information("Starting drive for task: {Task}", session.TaskDescription);
        try
        {
            return await _orchestration.StartDriveAsync(session);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to start drive");
            throw new DrivesNewServiceException("Failed to start drive", ex);
        }
    }

    public async Task<Drive> CreateDraftDriveAsync(AgentSession session)
    {
        _log.Debug("Creating draft drive for huddle conversation");
        try
        {
            return await _da.CreateDraftDriveAsync(session);
        }
        catch (Exception ex) when (ex is not DrivesNewServiceException)
        {
            _log.Error(ex, "Failed to create draft drive");
            throw new DrivesNewServiceException("Failed to create draft drive", ex);
        }
    }

    public Task AddTurnAsync(DriveTurn turn) => _da.AddTurnAsync(turn);

    /// <summary>Read-only web + filesystem tools for research runs, allowlisted so a headless run never blocks on a prompt.</summary>
    private static readonly List<string> ResearchTools = ["WebSearch", "WebFetch", "Read", "Glob", "Grep"];

    public IAsyncEnumerable<string> StreamQbResponseAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default)
    {
        _log.Debug("Streaming QB response via {ProviderId} with model {ModelId} effort {Effort}", providerId, modelId, effort);
        return StreamViaProviderAsync(providerId, new AgentContext
        {
            ModelId = modelId,
            SystemPrompt = systemPrompt,
            Prompt = prompt,
            WorkingDirectory = workingDir,
            Effort = effort,
            DisallowedTools = AgentDefaults.BlockedSubagentTools,
        }, ct);
    }

    public IAsyncEnumerable<string> StreamCoordinatorResearchAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default)
    {
        _log.Debug("Streaming research via {ProviderId} with model {ModelId}", providerId, modelId);
        return StreamViaProviderAsync(providerId, new AgentContext
        {
            ModelId = modelId,
            SystemPrompt = systemPrompt,
            Prompt = prompt,
            WorkingDirectory = workingDir,
            Effort = effort,
            AllowedTools = ResearchTools,
            DangerouslySkipPermissions = true,
            DisallowedTools = AgentDefaults.BlockedSubagentTools,
        }, ct);
    }

    /// <summary>
    /// Streams text through the drive's selected provider. The huddle used to call the
    /// Claude CLI directly, so a Codex drive still planned on Claude; resolving the
    /// provider here keeps planning on whichever backend the user picked.
    /// </summary>
    private async IAsyncEnumerable<string> StreamViaProviderAsync(
        string? providerId,
        AgentContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var provider = ResolveProvider(providerId);

        await foreach (var chunk in provider.StreamAsync(context, ct))
        {
            if (chunk.TextDelta != null)
                yield return chunk.TextDelta;
            else if (chunk.IsError && !string.IsNullOrWhiteSpace(chunk.Result))
                // Surface CLI failures instead of swallowing them — otherwise the huddle
                // shows an empty bubble and the agent looks like it never started.
                yield return $"\n\n⚠️ {chunk.Result}";
        }
    }

    private IAgentProvider ResolveProvider(string? providerId)
    {
        if (!string.IsNullOrEmpty(providerId))
        {
            try
            {
                return _providerRegistry.GetById(providerId);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unknown provider {ProviderId}; falling back to Claude Code", providerId);
            }
        }

        return _providerRegistry.GetById("claude-code");
    }
}
