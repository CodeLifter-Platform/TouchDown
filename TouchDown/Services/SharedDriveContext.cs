using System.Collections.Concurrent;
using System.Text.Json;

namespace TD.Services;

/// <summary>
/// Manages shared context between agents within a single Drive.
/// Each drive gets a .touchdown/ directory in the workspace where agents exchange:
/// - Plan (quarterback-plan.json): the structured plan from the Huddle
/// - Agent outputs (agent-outputs/{agentName}.md): each agent's completed work summary
/// - Shared artifacts (shared/): files agents want to share (interfaces, contracts, etc.)
/// </summary>
public interface ISharedDriveContext
{
    Task InitializeAsync(string driveId, string workspacePath, CancellationToken ct = default);
    Task WritePlanAsync(string driveId, string planJson, CancellationToken ct = default);
    Task<string?> ReadPlanAsync(string driveId, CancellationToken ct = default);
    Task WriteAgentOutputAsync(string driveId, string agentName, string output, CancellationToken ct = default);
    Task<string?> ReadAgentOutputAsync(string driveId, string agentName, CancellationToken ct = default);
    Task<Dictionary<string, string>> ReadAllAgentOutputsAsync(string driveId, CancellationToken ct = default);
    Task WriteSharedArtifactAsync(string driveId, string fileName, string content, CancellationToken ct = default);
    string GetContextDir(string driveId);
    string BuildAgentContextPrompt(string driveId, string agentName, string assignment, Dictionary<string, string>? priorOutputs = null);
}

public class SharedDriveContext : ISharedDriveContext
{
    private readonly ConcurrentDictionary<string, string> _driveWorkspaces = new();
    private readonly ILogger<SharedDriveContext> _logger;

    public SharedDriveContext(ILogger<SharedDriveContext> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(string driveId, string workspacePath, CancellationToken ct = default)
    {
        _driveWorkspaces[driveId] = workspacePath;

        var contextDir = GetContextDir(driveId);
        Directory.CreateDirectory(contextDir);
        Directory.CreateDirectory(Path.Combine(contextDir, "agent-outputs"));
        Directory.CreateDirectory(Path.Combine(contextDir, "shared"));

        _logger.LogInformation("Initialized shared context for drive {DriveId} at {Path}", driveId, contextDir);
        return Task.CompletedTask;
    }

    public async Task WritePlanAsync(string driveId, string planJson, CancellationToken ct = default)
    {
        var path = Path.Combine(GetContextDir(driveId), "quarterback-plan.json");
        await File.WriteAllTextAsync(path, planJson, ct);
    }

    public async Task<string?> ReadPlanAsync(string driveId, CancellationToken ct = default)
    {
        var path = Path.Combine(GetContextDir(driveId), "quarterback-plan.json");
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task WriteAgentOutputAsync(string driveId, string agentName, string output, CancellationToken ct = default)
    {
        var safeName = SanitizeFileName(agentName);
        var path = Path.Combine(GetContextDir(driveId), "agent-outputs", $"{safeName}.md");
        await File.WriteAllTextAsync(path, output, ct);
        _logger.LogInformation("Agent {Agent} wrote output for drive {DriveId} ({Bytes} bytes)", agentName, driveId, output.Length);
    }

    public async Task<string?> ReadAgentOutputAsync(string driveId, string agentName, CancellationToken ct = default)
    {
        var safeName = SanitizeFileName(agentName);
        var path = Path.Combine(GetContextDir(driveId), "agent-outputs", $"{safeName}.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task<Dictionary<string, string>> ReadAllAgentOutputsAsync(string driveId, CancellationToken ct = default)
    {
        var outputs = new Dictionary<string, string>();
        var dir = Path.Combine(GetContextDir(driveId), "agent-outputs");
        if (!Directory.Exists(dir)) return outputs;

        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            outputs[name] = await File.ReadAllTextAsync(file, ct);
        }

        return outputs;
    }

    public async Task WriteSharedArtifactAsync(string driveId, string fileName, string content, CancellationToken ct = default)
    {
        var path = Path.Combine(GetContextDir(driveId), "shared", SanitizeFileName(fileName));
        await File.WriteAllTextAsync(path, content, ct);
    }

    public string GetContextDir(string driveId)
    {
        if (_driveWorkspaces.TryGetValue(driveId, out var workspace))
            return Path.Combine(workspace, ".touchdown");

        return Path.Combine(Path.GetTempPath(), "touchdown", driveId, ".touchdown");
    }

    /// <summary>
    /// Builds the full prompt an agent receives, including context from the plan and prior agent outputs.
    /// </summary>
    public string BuildAgentContextPrompt(string driveId, string agentName, string assignment,
        Dictionary<string, string>? priorOutputs = null)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Your Assignment");
        sb.AppendLine(assignment);
        sb.AppendLine();

        // Include plan context
        var planPath = Path.Combine(GetContextDir(driveId), "quarterback-plan.json");
        if (File.Exists(planPath))
        {
            sb.AppendLine("# Team Plan (from the Quarterback)");
            sb.AppendLine("The Quarterback created this plan. Your assignment above is your specific part.");
            sb.AppendLine("```json");
            sb.AppendLine(File.ReadAllText(planPath));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Include prior agent outputs if available (for Safety review, etc.)
        if (priorOutputs is { Count: > 0 })
        {
            sb.AppendLine("# Prior Agent Outputs");
            sb.AppendLine("These are the outputs from other agents who completed their work before you:");
            sb.AppendLine();

            foreach (var (name, output) in priorOutputs)
            {
                sb.AppendLine($"## {name}");
                // Truncate very long outputs to keep context manageable
                var truncated = output.Length > 8000 ? output[..8000] + "\n\n[... truncated ...]" : output;
                sb.AppendLine(truncated);
                sb.AppendLine();
            }
        }

        // Include shared artifacts
        var sharedDir = Path.Combine(GetContextDir(driveId), "shared");
        if (Directory.Exists(sharedDir))
        {
            var files = Directory.GetFiles(sharedDir);
            if (files.Length > 0)
            {
                sb.AppendLine("# Shared Artifacts");
                sb.AppendLine("These files were shared by other agents for coordination:");
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    if (content.Length > 4000) content = content[..4000] + "\n[... truncated ...]";
                    sb.AppendLine($"## {Path.GetFileName(file)}");
                    sb.AppendLine("```");
                    sb.AppendLine(content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "-").ToLowerInvariant();
}
