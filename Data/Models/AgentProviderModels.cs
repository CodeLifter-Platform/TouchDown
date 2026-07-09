namespace TD.Models;

/// <summary>
/// Describes a single model offered by an <see cref="IAgentProvider"/>.
/// </summary>
public record AgentModel
{
    public required string ModelId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Provider-agnostic input for a single agent run.
/// </summary>
public record AgentContext
{
    public required string ModelId { get; init; }
    public string? SystemPrompt { get; init; }
    public required string Prompt { get; init; }
    public string? WorkingDirectory { get; init; }
    public List<string>? AllowedTools { get; init; }
    public bool DangerouslySkipPermissions { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public double? MaxBudgetUsd { get; init; }

    /// <summary>Reasoning effort level (provider-specific CLI value, e.g. "low"|"medium"|"high"|"xhigh"|"max").</summary>
    public string? Effort { get; init; }

    /// <summary>Tools the agent is forbidden from using (e.g. "Task"/"Agent" to block subagent spawning).</summary>
    public List<string>? DisallowedTools { get; init; }
}

/// <summary>
/// A single streaming chunk emitted by <see cref="IAgentProvider.StreamAsync"/>.
/// </summary>
public record AgentStreamChunk
{
    public string? TextDelta { get; init; }
    public string? ToolName { get; init; }
    public bool IsComplete { get; init; }
    public bool IsError { get; init; }
    public string? Result { get; init; }
    public double? CostUsd { get; init; }
}

/// <summary>
/// Aggregated result returned by <see cref="IAgentProvider.RunAsync"/>.
/// </summary>
public record AgentResponse
{
    public string FullText { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public double? CostUsd { get; init; }
    public long? DurationMs { get; init; }
    public int? NumTurns { get; init; }
    public List<string> ToolsUsed { get; init; } = [];
}

