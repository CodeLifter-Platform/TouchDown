using TD.Models;

namespace TD.Services;

public record ClaudeRunOptions
{
    public required string ModelId { get; init; }
    public string? SystemPrompt { get; init; }
    public required string Prompt { get; init; }
    public string? WorkingDirectory { get; init; }
    public List<string>? AllowedTools { get; init; }
    public bool DangerouslySkipPermissions { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public double? MaxBudgetUsd { get; init; }

    /// <summary>Effort level passed to <c>claude --effort</c> (low|medium|high|xhigh|max).</summary>
    public string? Effort { get; init; }

    /// <summary>Tools blocked via <c>--disallowedTools</c> (e.g. "Task"/"Agent" to prevent subagent spawning).</summary>
    public List<string>? DisallowedTools { get; init; }
}

public record ClaudeStreamChunk
{
    public ClaudeStreamEvent RawEvent { get; init; } = null!;
    public string? TextDelta { get; init; }
    public string? ToolName { get; init; }
    public bool IsComplete { get; init; }
    public bool IsError { get; init; }
    public string? Result { get; init; }
    public double? CostUsd { get; init; }
}

public interface IClaudeStreamingService
{
    IAsyncEnumerable<ClaudeStreamChunk> StreamAsync(
        ClaudeRunOptions options,
        CancellationToken cancellationToken = default);

    Task<ClaudeResult> RunAsync(
        ClaudeRunOptions options,
        Func<ClaudeStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamResponseAsync(
        string modelId,
        string systemPrompt,
        string userMessage,
        string? workingDirectory = null,
        string? effort = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a research run with web tools enabled (WebSearch/WebFetch + read-only filesystem).</summary>
    IAsyncEnumerable<string> StreamResearchAsync(
        string modelId,
        string systemPrompt,
        string prompt,
        string? workingDirectory = null,
        string? effort = null,
        CancellationToken cancellationToken = default);

    Task<string> GetFullResponseAsync(
        string modelId,
        string systemPrompt,
        string userMessage,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}

public record ClaudeResult
{
    public string FullText { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public double? CostUsd { get; init; }
    public long? DurationMs { get; init; }
    public int? NumTurns { get; init; }
    public List<string> ToolsUsed { get; init; } = [];
}
