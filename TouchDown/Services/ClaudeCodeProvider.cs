using System.Runtime.CompilerServices;
using System.Text;
using TD.Models;

namespace TD.Services;

/// <summary>
/// IAgentProvider implementation backed by the Claude Code CLI.
/// Wraps the lower-level ClaudeStreamingService so all Claude-specific
/// protocol details stay in one place.
/// </summary>
public class ClaudeCodeProvider : IAgentProvider
{
    private readonly IClaudeStreamingService _claude;
    private readonly IClaudeHealthCheck _health;
    private readonly ILogger<ClaudeCodeProvider> _logger;

    // ── Provider identity ────────────────────────────────────────
    public string ProviderId => "claude-code";
    public string DisplayName => "Claude Code (Anthropic)";

    public IReadOnlyList<AgentModel> AvailableModels { get; } =
    [
        new AgentModel { ModelId = "claude-opus-4-8",   DisplayName = "Claude Opus 4.8",   Description = "Most capable — best for complex reasoning" },
        new AgentModel { ModelId = "claude-opus-4-7",   DisplayName = "Claude Opus 4.7",   Description = "Previous-gen Opus — strong agentic work" },
        new AgentModel { ModelId = "claude-sonnet-4-6", DisplayName = "Claude Sonnet 4.6", Description = "Balanced speed & capability" },
        new AgentModel { ModelId = "claude-haiku-4-5",  DisplayName = "Claude Haiku 4.5",  Description = "Fastest — best for simple tasks" },
    ];

    public ClaudeCodeProvider(
        IClaudeStreamingService claude,
        IClaudeHealthCheck health,
        ILogger<ClaudeCodeProvider> logger)
    {
        _claude = claude;
        _health = health;
        _logger = logger;
    }

    // ── IAgentProvider ───────────────────────────────────────────

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var status = await _health.CheckAsync(ct);
        return status.IsHealthy;
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
        AgentContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in _claude.StreamAsync(MapToRunOptions(context), ct))
        {
            yield return MapChunk(chunk);
        }
    }

    public async Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default)
    {
        var fullText = new StringBuilder();
        var toolsUsed = new List<string>();
        double? costUsd = null;
        bool isError = false;

        await foreach (var chunk in StreamAsync(context, ct))
        {
            if (chunk.TextDelta != null)
                fullText.Append(chunk.TextDelta);

            if (chunk.ToolName != null && !toolsUsed.Contains(chunk.ToolName))
                toolsUsed.Add(chunk.ToolName);

            if (chunk.CostUsd.HasValue)
                costUsd = chunk.CostUsd;

            if (chunk.IsError)
                isError = true;

            if (chunk.IsComplete && chunk.Result != null && fullText.Length == 0)
                fullText.Append(chunk.Result);
        }

        return new AgentResponse
        {
            FullText = fullText.ToString(),
            IsError = isError,
            CostUsd = costUsd,
            ToolsUsed = toolsUsed
        };
    }

    // ── Mapping helpers ──────────────────────────────────────────

    private static ClaudeRunOptions MapToRunOptions(AgentContext ctx) => new()
    {
        ModelId = ctx.ModelId,
        SystemPrompt = ctx.SystemPrompt,
        Prompt = ctx.Prompt,
        WorkingDirectory = ctx.WorkingDirectory,
        AllowedTools = ctx.AllowedTools,
        DangerouslySkipPermissions = ctx.DangerouslySkipPermissions,
        AppendSystemPrompt = ctx.AppendSystemPrompt,
        MaxBudgetUsd = ctx.MaxBudgetUsd,
        Effort = ctx.Effort,
        DisallowedTools = ctx.DisallowedTools,
    };

    private static AgentStreamChunk MapChunk(ClaudeStreamChunk c) => new()
    {
        TextDelta = c.TextDelta,
        ToolName = c.ToolName,
        IsComplete = c.IsComplete,
        IsError = c.IsError,
        Result = c.Result,
        CostUsd = c.CostUsd,
    };
}

