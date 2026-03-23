using TouchDown.Models;

namespace TouchDown.Services;

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
