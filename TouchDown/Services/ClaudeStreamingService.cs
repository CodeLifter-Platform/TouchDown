using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TD.Models;

namespace TD.Services;

public class ClaudeStreamingService : IClaudeStreamingService
{
    private readonly ILogger<ClaudeStreamingService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ClaudeStreamingService(ILogger<ClaudeStreamingService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<ClaudeStreamChunk> StreamAsync(
        ClaudeRunOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var process = CreateProcess(options);
        process.Start();
        _logger.LogInformation("Claude process started (PID {Pid}) model={Model}", process.Id, options.ModelId);

        // Write prompt to stdin and close
        await process.StandardInput.WriteLineAsync(options.Prompt);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();

        // Read stderr in background for error reporting
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Parse stream-json: each line is a JSON event
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line == null) break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            ClaudeStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<ClaudeStreamEvent>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Failed to parse stream-json line: {Error}\nLine: {Line}", ex.Message, line);
                continue;
            }

            if (evt == null) continue;

            var chunk = MapEventToChunk(evt);
            if (chunk != null)
                yield return chunk;
        }

        if (!process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            _logger.LogError("Claude process (PID {Pid}) exited with code {Code}: {Stderr}", process.Id, process.ExitCode, stderr);

            yield return new ClaudeStreamChunk
            {
                RawEvent = new ClaudeStreamEvent { Type = "error" },
                IsError = true,
                Result = $"Process exited with code {process.ExitCode}: {stderr}"
            };
        }
        else
        {
            _logger.LogInformation("Claude process (PID {Pid}) completed successfully", process.Id);
        }
    }

    public async Task<ClaudeResult> RunAsync(
        ClaudeRunOptions options,
        Func<ClaudeStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        var fullText = new StringBuilder();
        var toolsUsed = new List<string>();
        double? cost = null;
        long? duration = null;
        int? turns = null;
        bool isError = false;

        await foreach (var chunk in StreamAsync(options, cancellationToken))
        {
            if (chunk.TextDelta != null)
                fullText.Append(chunk.TextDelta);

            if (chunk.ToolName != null && !toolsUsed.Contains(chunk.ToolName))
                toolsUsed.Add(chunk.ToolName);

            if (chunk.CostUsd.HasValue)
                cost = chunk.CostUsd;

            if (chunk.IsError)
                isError = true;

            if (chunk.IsComplete && chunk.Result != null)
            {
                // The result event contains the final text
                if (fullText.Length == 0)
                    fullText.Append(chunk.Result);
            }

            if (onChunk != null)
                await onChunk(chunk);
        }

        return new ClaudeResult
        {
            FullText = fullText.ToString(),
            IsError = isError,
            CostUsd = cost,
            DurationMs = duration,
            NumTurns = turns,
            ToolsUsed = toolsUsed
        };
    }

    // Legacy interface for backward compatibility (PlanningModal etc.)
    public async IAsyncEnumerable<string> StreamResponseAsync(
        string modelId,
        string systemPrompt,
        string userMessage,
        string? workingDirectory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var options = new ClaudeRunOptions
        {
            ModelId = modelId,
            SystemPrompt = systemPrompt,
            Prompt = userMessage,
            WorkingDirectory = workingDirectory
        };

        await foreach (var chunk in StreamAsync(options, cancellationToken))
        {
            if (chunk.TextDelta != null)
                yield return chunk.TextDelta;
        }
    }

    public async Task<string> GetFullResponseAsync(
        string modelId,
        string systemPrompt,
        string userMessage,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(new ClaudeRunOptions
        {
            ModelId = modelId,
            SystemPrompt = systemPrompt,
            Prompt = userMessage,
            WorkingDirectory = workingDirectory
        }, cancellationToken: cancellationToken);

        return result.FullText;
    }

    private static ClaudeStreamChunk? MapEventToChunk(ClaudeStreamEvent evt)
    {
        return evt.Type switch
        {
            // Text streaming
            "content_block_delta" when evt.Delta?.Text != null => new ClaudeStreamChunk
            {
                RawEvent = evt,
                TextDelta = evt.Delta.Text
            },

            // Tool use started
            "content_block_start" when evt.ContentBlock?.Type == "tool_use" => new ClaudeStreamChunk
            {
                RawEvent = evt,
                ToolName = evt.ContentBlock.Name
            },

            // Message complete / result
            "result" => new ClaudeStreamChunk
            {
                RawEvent = evt,
                IsComplete = true,
                IsError = evt.IsError ?? false,
                Result = evt.Result,
                CostUsd = evt.CostUsd
            },

            // Ignore system, message_start, message_delta, content_block_start (text), content_block_stop, etc.
            _ => null
        };
    }

    private Process CreateProcess(ClaudeRunOptions options)
    {
        var args = new List<string> { "--print", "--output-format", "stream-json", "--model", options.ModelId };

        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            args.Add("--system-prompt");
            args.Add(options.SystemPrompt);
        }

        if (!string.IsNullOrEmpty(options.AppendSystemPrompt))
        {
            args.Add("--append-system-prompt");
            args.Add(options.AppendSystemPrompt);
        }

        if (options.AllowedTools is { Count: > 0 })
        {
            args.Add("--allowedTools");
            args.AddRange(options.AllowedTools);
        }

        if (options.DangerouslySkipPermissions)
        {
            args.Add("--dangerously-skip-permissions");
        }

        if (options.MaxBudgetUsd.HasValue)
        {
            args.Add("--max-budget-usd");
            args.Add(options.MaxBudgetUsd.Value.ToString("F2"));
        }

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            WorkingDirectory = options.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Clear the CLAUDECODE env var so nested sessions work
        psi.Environment["CLAUDECODE"] = "";

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        return new Process { StartInfo = psi };
    }
}
