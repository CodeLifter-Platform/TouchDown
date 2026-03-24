using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TD.Models;

namespace TD.Services;

/// <summary>
/// IAgentProvider implementation backed by the OpenAI Codex CLI.
/// Runs 'codex exec --full-auto' for non-interactive automation and streams
/// stdout line-by-line, attempting JSON parsing for structured events first
/// and falling back to plain-text deltas.
/// </summary>
public class CodexProvider : IAgentProvider
{
    private readonly ICodexHealthCheck _health;
    private readonly ILogger<CodexProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // ── Provider identity ────────────────────────────────────────
    public string ProviderId => "codex";
    public string DisplayName => "OpenAI Codex";

    public IReadOnlyList<AgentModel> AvailableModels { get; } =
    [
        new AgentModel { ModelId = "gpt-5.4",             DisplayName = "GPT-5.4",              Description = "Flagship — strongest reasoning & coding" },
        new AgentModel { ModelId = "o4-mini",             DisplayName = "o4-mini",               Description = "Compact reasoning — fast & affordable" },
        new AgentModel { ModelId = "o3-mini",             DisplayName = "o3-mini",               Description = "Lightweight reasoning model" },
        new AgentModel { ModelId = "gpt-5.3-codex-spark", DisplayName = "GPT-5.3 Codex Spark",  Description = "Ultra-fast (Pro subscribers)" },
    ];

    public CodexProvider(ICodexHealthCheck health, ILogger<CodexProvider> logger)
    {
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
        var process = BuildProcess(context);
        process.Start();
        _logger.LogInformation("Codex process started (PID {Pid}) model={Model}", process.Id, context.ModelId);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try { line = await process.StandardOutput.ReadLineAsync(ct); }
            catch (OperationCanceledException) { break; }

            if (line == null) break;           // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = ParseLine(line);
            if (chunk != null)
                yield return chunk;
        }

        if (!process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            _logger.LogError("Codex process (PID {Pid}) exited {Code}: {Err}", process.Id, process.ExitCode, stderr);
            yield return new AgentStreamChunk
            {
                IsComplete = true,
                IsError = true,
                Result = $"codex exited with code {process.ExitCode}: {stderr}"
            };
        }
        else
        {
            _logger.LogInformation("Codex process (PID {Pid}) completed successfully", process.Id);
            yield return new AgentStreamChunk { IsComplete = true };
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
            if (chunk.TextDelta != null) fullText.Append(chunk.TextDelta);
            if (chunk.ToolName != null && !toolsUsed.Contains(chunk.ToolName)) toolsUsed.Add(chunk.ToolName);
            if (chunk.CostUsd.HasValue) costUsd = chunk.CostUsd;
            if (chunk.IsError) isError = true;
            if (chunk.IsComplete && chunk.Result != null && fullText.Length == 0) fullText.Append(chunk.Result);
        }

        return new AgentResponse { FullText = fullText.ToString(), IsError = isError, CostUsd = costUsd, ToolsUsed = toolsUsed };
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Each stdout line is either a JSON event (structured) or plain text.
    /// We try JSON first; anything that doesn't parse becomes a text delta.
    /// </summary>
    private AgentStreamChunk? ParseLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Codex exec --json emits events with a "type" discriminator
            if (root.TryGetProperty("type", out var typeProp))
            {
                return typeProp.GetString() switch
                {
                    "text"       => new AgentStreamChunk { TextDelta    = root.TryGetProperty("content", out var c) ? c.GetString() : line },
                    "tool_call"  => new AgentStreamChunk { ToolName     = root.TryGetProperty("name",    out var n) ? n.GetString() : "tool" },
                    "result"     => new AgentStreamChunk { IsComplete   = true, Result = root.TryGetProperty("content", out var r) ? r.GetString() : null },
                    "error"      => new AgentStreamChunk { IsComplete   = true, IsError = true, Result = root.TryGetProperty("message", out var m) ? m.GetString() : line },
                    _            => null   // ignore unknown structured events
                };
            }

            // JSON but no known discriminator — treat content as text
            return new AgentStreamChunk { TextDelta = line };
        }
        catch (JsonException)
        {
            // Plain text line from the CLI → emit as text delta
            return new AgentStreamChunk { TextDelta = line + "\n" };
        }
    }

    private Process BuildProcess(AgentContext ctx)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "codex",
            WorkingDirectory = ctx.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute  = false,
            CreateNoWindow   = true,
        };

        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add("--full-auto");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(ctx.ModelId);

        if (!string.IsNullOrEmpty(ctx.WorkingDirectory))
        {
            psi.ArgumentList.Add("--cd");
            psi.ArgumentList.Add(ctx.WorkingDirectory);
        }

        // Embed system prompt as a preamble when the provider doesn't have a native flag
        var prompt = string.IsNullOrEmpty(ctx.SystemPrompt)
            ? ctx.Prompt
            : $"[System instructions]\n{ctx.SystemPrompt}\n\n[Task]\n{ctx.Prompt}";

        psi.ArgumentList.Add(prompt);

        return new Process { StartInfo = psi };
    }
}

