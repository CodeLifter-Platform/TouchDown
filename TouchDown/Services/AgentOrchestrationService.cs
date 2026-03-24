using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TD.Data;
using TD.Hubs;
using TD.Models;

namespace TD.Services;

public interface IAgentOrchestrationService
{
    Task<Drive> StartDriveAsync(AgentSession session, CancellationToken ct = default);
    Task CancelDriveAsync(string driveId, CancellationToken ct = default);
    Task<Drive?> GetDriveAsync(string driveId, CancellationToken ct = default);
    Task<List<Drive>> GetRecentDrivesAsync(int count = 20, CancellationToken ct = default);
}

public class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly IDbContextFactory<TDDbContext> _dbFactory;
    private readonly IAgentProvider _provider;
    private readonly IGitWorktreeService _git;
    private readonly ISharedDriveContext _context;
    private readonly IPlanParserService _planParser;
    private readonly IHubContext<AgentHub> _hub;
    private readonly ILogger<AgentOrchestrationService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeDrives = new();

    public AgentOrchestrationService(
        IDbContextFactory<TDDbContext> dbFactory,
        IAgentProvider provider,
        IGitWorktreeService git,
        ISharedDriveContext context,
        IPlanParserService planParser,
        IHubContext<AgentHub> hub,
        ILogger<AgentOrchestrationService> logger)
    {
        _dbFactory = dbFactory;
        _provider = provider;
        _git = git;
        _context = context;
        _planParser = planParser;
        _hub = hub;
        _logger = logger;
    }

    public async Task<Drive> StartDriveAsync(AgentSession session, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var drive = new Drive
        {
            TaskDescription = session.TaskDescription ?? string.Empty,
            MaxParallelism = session.MaxParallelism,
            SourceType = session.SourceType,
            RepoPath = session.RepoPath,
            Branch = session.Branch,
            WorkspaceMode = session.WorkspaceMode,
            WorkspacePath = session.WorkspaceMode == WorkspaceMode.FreshFolder
                ? Path.Combine(Path.GetTempPath(), "touchdown", session.FreshFolderName ?? session.SessionId)
                : session.RepoPath,
            PrBranchName = session.PrBranchName,
            AgentTeamId = session.Team.Id,
            HuddlePlan = session.Drive.HuddlePlan,
            Status = DriveStatus.InProgress
        };

        db.Drives.Add(drive);
        await db.SaveChangesAsync(ct);

        // Load the full team with members for execution
        var team = await db.AgentTeams
            .Include(t => t.Members)
            .Include(t => t.CommunicationRules)
            .FirstAsync(t => t.Id == session.Team.Id, ct);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeDrives[drive.DriveId] = cts;

        _ = Task.Run(() => ExecuteDriveAsync(drive, team, cts.Token), cts.Token);

        return drive;
    }

    public async Task CancelDriveAsync(string driveId, CancellationToken ct = default)
    {
        if (_activeDrives.TryRemove(driveId, out var cts))
        {
            await cts.CancelAsync();
            _logger.LogInformation("Cancelled drive {DriveId}", driveId);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var drive = await db.Drives.FirstOrDefaultAsync(d => d.DriveId == driveId, ct);
            if (drive != null)
            {
                drive.Status = DriveStatus.Cancelled;
                drive.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            await _hub.Clients.Group(driveId).SendAsync("DriveCompleted",
                new { DriveId = driveId, Status = "Cancelled" }, ct);
        }
    }

    public async Task<Drive?> GetDriveAsync(string driveId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Drives
            .Include(d => d.AgentTeam).ThenInclude(t => t!.Members)
            .Include(d => d.Plays).ThenInclude(p => p.AssignedMember)
            .Include(d => d.Logs)
            .FirstOrDefaultAsync(d => d.DriveId == driveId, ct);
    }

    public async Task<List<Drive>> GetRecentDrivesAsync(int count = 20, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Drives
            .Include(d => d.AgentTeam)
            .OrderByDescending(d => d.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────
    // Drive Execution Engine
    // ──────────────────────────────────────────────────────────────

    private async Task ExecuteDriveAsync(Drive drive, AgentTeam team, CancellationToken ct)
    {
        try
        {
            // Phase 0: Prepare workspace
            var workDir = await PrepareWorkspaceAsync(drive, ct);
            await _context.InitializeAsync(drive.DriveId, workDir, ct);

            await SendLog(drive.DriveId, "System", $"Drive started. Workspace: {workDir}", ct);
            await SendLog(drive.DriveId, "System", $"Team: {team.Name} ({team.Members.Count} agents)", ct);

            // Phase 1: Parse the Huddle plan into structured assignments
            await SendLog(drive.DriveId, "Quarterback", "Parsing the Huddle plan into structured assignments...", ct);

            var plan = await _planParser.ParsePlanFromHuddleAsync(
                drive.HuddlePlan ?? drive.TaskDescription,
                team,
                drive.TaskDescription,
                _provider,
                workDir,
                ct);

            // Save plan to shared context
            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            await _context.WritePlanAsync(drive.DriveId, planJson, ct);

            await SendLog(drive.DriveId, "Quarterback",
                $"Plan ready: {plan.Summary} ({plan.Assignments.Count} assignments)", ct);

            // Phase 2: Convert plan to plays and save to DB
            var plays = _planParser.ConvertPlanToPlays(plan, team);
            await SavePlays(drive.Id, plays, ct);

            foreach (var play in plays)
            {
                await SendLog(drive.DriveId, play.AssignedMember?.Name ?? "Unknown",
                    $"Assigned: {TruncateForLog(play.Description)}", ct);
            }

            // Phase 3: Execute plays respecting dependencies
            await ExecutePlaysWithDependencies(drive, plays, plan, team, workDir, ct);

            // Phase 4: Mark Touchdown
            await MarkDriveStatus(drive, DriveStatus.Touchdown, ct);
            await _hub.Clients.Group(drive.DriveId).SendAsync("DriveCompleted",
                new { drive.DriveId, Status = "Touchdown" }, ct);
            await SendLog(drive.DriveId, "System", "TOUCHDOWN! Drive completed successfully.", ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Drive {DriveId} was cancelled", drive.DriveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drive {DriveId} failed", drive.DriveId);
            await MarkDriveStatus(drive, DriveStatus.Turnover, ct);
            await _hub.Clients.Group(drive.DriveId).SendAsync("DriveCompleted",
                new { drive.DriveId, Status = "Turnover" }, ct);
            await SendLog(drive.DriveId, "System", $"TURNOVER! Drive failed: {ex.Message}", ct);
        }
        finally
        {
            _activeDrives.TryRemove(drive.DriveId, out _);
        }
    }

    /// <summary>
    /// Executes plays respecting the dependency graph from the QB plan.
    /// Plays with no dependencies (or whose dependencies are met) run in parallel,
    /// gated by the MaxParallelism semaphore.
    /// </summary>
    private async Task ExecutePlaysWithDependencies(
        Drive drive, List<Play> plays, QuarterbackPlan plan, AgentTeam team,
        string workDir, CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(drive.MaxParallelism);
        var completedIndices = new ConcurrentBag<int>();
        var playResults = new ConcurrentDictionary<int, PlayResult>();

        // Build dependency map from plan
        var dependencyMap = new Dictionary<int, List<int>>();
        for (int i = 0; i < plan.Assignments.Count && i < plays.Count; i++)
        {
            dependencyMap[i] = plan.Assignments[i].DependsOn ?? [];
        }

        // Group by waves: wave 0 = no deps, wave 1 = depends on wave 0, etc.
        var waves = BuildExecutionWaves(plays.Count, dependencyMap);

        foreach (var wave in waves)
        {
            await SendLog(drive.DriveId, "System",
                $"Executing wave: {string.Join(", ", wave.Select(i => plays[i].AssignedMember?.Name ?? $"Play {i}"))}", ct);

            var waveTasks = wave.Select(playIndex =>
                RunPlayWithContextAsync(drive, plays[playIndex], playIndex, workDir, semaphore, playResults, ct));

            await Task.WhenAll(waveTasks);

            foreach (var idx in wave)
                completedIndices.Add(idx);
        }
    }

    private async Task RunPlayWithContextAsync(
        Drive drive, Play play, int playIndex, string workDir,
        SemaphoreSlim semaphore, ConcurrentDictionary<int, PlayResult> results,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            var member = play.AssignedMember!;
            await UpdateAgentStatus(drive.DriveId, member.Name, "Running", 0, ct);
            await SendLog(drive.DriveId, member.Name, $"Starting: {TruncateForLog(play.Description)}", ct);

            play.Status = PlayStatus.InProgress;
            play.StartedAt = DateTime.UtcNow;

            // Build context-aware prompt: include plan + prior outputs for validators
            Dictionary<string, string>? priorOutputs = null;
            if (member.Role == AgentRole.Validator)
            {
                priorOutputs = await _context.ReadAllAgentOutputsAsync(drive.DriveId, ct);
            }

            var fullPrompt = _context.BuildAgentContextPrompt(
                drive.DriveId, member.Name, play.Description, priorOutputs);

            // Stream provider output, forwarding chunks to the UI in real time
            var fullText = new System.Text.StringBuilder();
            var toolsUsed = new List<string>();
            double? costUsd = null;
            bool isError = false;

            await foreach (var chunk in _provider.StreamAsync(
                new AgentContext
                {
                    ModelId = member.Model.ToModelId(),
                    SystemPrompt = member.SystemPrompt,
                    Prompt = fullPrompt,
                    WorkingDirectory = workDir,
                    DangerouslySkipPermissions = true,
                    AllowedTools = GetToolsForRole(member.Role),
                }, ct))
            {
                if (chunk.TextDelta != null)
                {
                    fullText.Append(chunk.TextDelta);
                    if (chunk.TextDelta.Contains('\n'))
                        await SendLog(drive.DriveId, member.Name, chunk.TextDelta.Trim(), ct);
                }

                if (chunk.ToolName != null)
                {
                    if (!toolsUsed.Contains(chunk.ToolName))
                        toolsUsed.Add(chunk.ToolName);
                    await SendLog(drive.DriveId, member.Name, $"Using tool: {chunk.ToolName}", ct);
                }

                if (chunk.CostUsd.HasValue) costUsd = chunk.CostUsd;
                if (chunk.IsError) isError = true;
                if (chunk.IsComplete && chunk.Result != null && fullText.Length == 0)
                    fullText.Append(chunk.Result);
            }

            var result = new AgentResponse
            {
                FullText = fullText.ToString(),
                IsError = isError,
                CostUsd = costUsd,
                ToolsUsed = toolsUsed,
            };

            if (result.IsError)
            {
                play.Status = PlayStatus.Failed;
                play.Output = result.FullText;
                play.CompletedAt = DateTime.UtcNow;
                await SendLog(drive.DriveId, member.Name, $"FAILED: {TruncateForLog(result.FullText)}", ct);
                await UpdateAgentStatus(drive.DriveId, member.Name, "Failed", 100, ct);
                await SavePlayStatus(play, ct);
                throw new InvalidOperationException($"Agent {member.Name} failed: {result.FullText}");
            }

            play.Output = result.FullText;
            play.Status = PlayStatus.Completed;
            play.CompletedAt = DateTime.UtcNow;

            // Write output to shared context so other agents can see it
            await _context.WriteAgentOutputAsync(drive.DriveId, member.Name, result.FullText, ct);

            results[playIndex] = new PlayResult { Output = result.FullText, CostUsd = result.CostUsd ?? 0 };

            var costInfo = result.CostUsd.HasValue ? $" (${result.CostUsd:F4})" : "";
            var toolInfo = result.ToolsUsed.Count > 0 ? $" [tools: {string.Join(", ", result.ToolsUsed)}]" : "";
            await SendLog(drive.DriveId, member.Name, $"Completed{costInfo}{toolInfo}", ct);
            await UpdateAgentStatus(drive.DriveId, member.Name, "Completed", 100, ct);
            await SavePlayStatus(play, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            play.Status = PlayStatus.Failed;
            play.CompletedAt = DateTime.UtcNow;
            await SendLog(drive.DriveId, play.AssignedMember?.Name ?? "Unknown", $"Error: {ex.Message}", ct);
            await UpdateAgentStatus(drive.DriveId, play.AssignedMember?.Name ?? "Unknown", "Failed", 100, ct);
            await SavePlayStatus(play, ct);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static List<List<int>> BuildExecutionWaves(int playCount, Dictionary<int, List<int>> deps)
    {
        var waves = new List<List<int>>();
        var completed = new HashSet<int>();
        var remaining = Enumerable.Range(0, playCount).ToHashSet();

        while (remaining.Count > 0)
        {
            var wave = remaining
                .Where(i => !deps.ContainsKey(i) || deps[i].All(d => completed.Contains(d)))
                .ToList();

            if (wave.Count == 0)
            {
                // Circular dependency or orphaned plays — execute remaining as final wave
                wave = remaining.ToList();
            }

            waves.Add(wave);
            foreach (var i in wave)
            {
                remaining.Remove(i);
                completed.Add(i);
            }
        }

        return waves;
    }

    private static List<string> GetToolsForRole(AgentRole role) => role switch
    {
        AgentRole.Leader => ["Read", "Glob", "Grep", "Bash", "Edit", "Write"],
        AgentRole.Worker => ["Read", "Glob", "Grep", "Bash", "Edit", "Write"],
        AgentRole.Validator => ["Read", "Glob", "Grep", "Bash"],
        AgentRole.Tester => ["Read", "Glob", "Grep", "Bash", "Edit", "Write"],
        AgentRole.DevOps => ["Read", "Glob", "Grep", "Bash", "Edit", "Write"],
        _ => ["Read", "Glob", "Grep"]
    };

    private async Task<string> PrepareWorkspaceAsync(Drive drive, CancellationToken ct)
    {
        return drive.WorkspaceMode switch
        {
            WorkspaceMode.PrWorktree when !string.IsNullOrEmpty(drive.RepoPath) =>
                await _git.CreateWorktreeAsync(drive.RepoPath, drive.PrBranchName ?? $"touchdown/drive-{drive.DriveId}", ct),
            WorkspaceMode.FreshFolder =>
                CreateFreshFolder(drive.WorkspacePath ?? Path.Combine(Path.GetTempPath(), "touchdown", drive.DriveId)),
            _ => drive.RepoPath ?? Environment.CurrentDirectory
        };
    }

    private static string CreateFreshFolder(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task SavePlays(int driveId, List<Play> plays, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        foreach (var play in plays)
        {
            play.DriveId = driveId;
            db.Plays.Add(play);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SavePlayStatus(Play play, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Plays.FindAsync([play.Id], ct);
        if (entity != null)
        {
            entity.Status = play.Status;
            entity.StartedAt = play.StartedAt;
            entity.CompletedAt = play.CompletedAt;
            entity.Output = play.Output;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task MarkDriveStatus(Drive drive, DriveStatus status, CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var entity = await db.Drives.FindAsync([drive.Id], ct);
            if (entity != null)
            {
                entity.Status = status;
                entity.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark drive {DriveId} as {Status}", drive.DriveId, status);
        }
    }

    private async Task UpdateAgentStatus(string driveId, string agentName, string status, int progress, CancellationToken ct)
    {
        await _hub.Clients.Group(driveId).SendAsync("AgentStatusUpdate",
            new { AgentName = agentName, Status = status, ProgressPercent = progress }, ct);
    }

    private async Task SendLog(string driveId, string agentName, string message, CancellationToken ct)
    {
        await _hub.Clients.Group(driveId).SendAsync("ReceiveLog", new
        {
            Timestamp = DateTime.UtcNow,
            AgentName = agentName,
            Message = message,
            Level = "Info"
        }, ct);

        // Persist to DB
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var drive = await db.Drives.FirstOrDefaultAsync(d => d.DriveId == driveId, ct);
            if (drive != null)
            {
                db.DriveLogs.Add(new DriveLog
                {
                    AgentName = agentName,
                    Message = message,
                    DriveId = drive.Id
                });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist log for drive {DriveId}", driveId);
        }
    }

    private static string TruncateForLog(string text) =>
        text.Length > 150 ? text[..147] + "..." : text;

    private record PlayResult
    {
        public string Output { get; init; } = "";
        public double CostUsd { get; init; }
    }
}
