using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TD.Services;

public record ClaudeHealthStatus
{
    public bool IsInstalled { get; init; }
    public bool IsAuthenticated { get; init; }
    public string? Version { get; init; }
    public string? CliPath { get; init; }
    public string? Error { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    public bool IsHealthy => IsInstalled && IsAuthenticated;
}

public interface IClaudeHealthCheck
{
    Task<ClaudeHealthStatus> CheckAsync(CancellationToken ct = default);
    ClaudeHealthStatus? LastStatus { get; }
}

public class ClaudeHealthCheck : IClaudeHealthCheck, IHealthCheck
{
    private readonly ILogger<ClaudeHealthCheck> _logger;
    private ClaudeHealthStatus? _lastStatus;

    public ClaudeHealthCheck(ILogger<ClaudeHealthCheck> logger)
    {
        _logger = logger;
    }

    public ClaudeHealthStatus? LastStatus => _lastStatus;

    public async Task<ClaudeHealthStatus> CheckAsync(CancellationToken ct = default)
    {
        var status = await RunCheckAsync(ct);
        _lastStatus = status;
        return status;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var status = await CheckAsync(ct);

        if (status.IsHealthy)
        {
            return HealthCheckResult.Healthy(
                $"Claude Code v{status.Version} at {status.CliPath}");
        }

        if (status.IsInstalled && !status.IsAuthenticated)
        {
            return HealthCheckResult.Degraded(
                $"Claude Code is installed but not authenticated. Run 'claude auth' to set up your license. Error: {status.Error}");
        }

        return HealthCheckResult.Unhealthy(
            $"Claude Code CLI not found. Install it from https://docs.anthropic.com/en/docs/claude-code. Error: {status.Error}");
    }

    private async Task<ClaudeHealthStatus> RunCheckAsync(CancellationToken ct)
    {
        // Step 1: Check if claude is on PATH
        string? cliPath;
        try
        {
            cliPath = await RunCommandAsync("which", "claude", ct);
            if (string.IsNullOrWhiteSpace(cliPath))
            {
                return new ClaudeHealthStatus
                {
                    IsInstalled = false,
                    Error = "'claude' not found on PATH"
                };
            }
            cliPath = cliPath.Trim();
        }
        catch (Exception ex)
        {
            return new ClaudeHealthStatus
            {
                IsInstalled = false,
                Error = $"Failed to locate claude: {ex.Message}"
            };
        }

        // Step 2: Get version
        string? version;
        try
        {
            version = (await RunCommandAsync("claude", "--version", ct))?.Trim();
        }
        catch (Exception ex)
        {
            return new ClaudeHealthStatus
            {
                IsInstalled = true,
                CliPath = cliPath,
                Error = $"Failed to get version: {ex.Message}"
            };
        }

        // Step 3: Check authentication by running a minimal prompt
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--print");
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add("haiku");
            psi.ArgumentList.Add("--max-budget-usd");
            psi.ArgumentList.Add("0.01");
            psi.ArgumentList.Add("--output-format");
            psi.ArgumentList.Add("json");
            psi.Environment["CLAUDECODE"] = "";

            var process = new Process { StartInfo = psi };
            process.Start();

            await process.StandardInput.WriteLineAsync("Respond with exactly: ok");
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                var errorMsg = stderr.Length > 200 ? stderr[..200] : stderr;
                _logger.LogWarning("Claude auth check failed (exit {Code}): {Error}", process.ExitCode, errorMsg);

                return new ClaudeHealthStatus
                {
                    IsInstalled = true,
                    IsAuthenticated = false,
                    Version = version,
                    CliPath = cliPath,
                    Error = $"Auth check failed (exit {process.ExitCode}): {errorMsg}"
                };
            }

            _logger.LogInformation("Claude health check passed: v{Version} at {Path}", version, cliPath);

            return new ClaudeHealthStatus
            {
                IsInstalled = true,
                IsAuthenticated = true,
                Version = version,
                CliPath = cliPath,
            };
        }
        catch (OperationCanceledException)
        {
            return new ClaudeHealthStatus
            {
                IsInstalled = true,
                IsAuthenticated = false,
                Version = version,
                CliPath = cliPath,
                Error = "Auth check timed out after 30s"
            };
        }
        catch (Exception ex)
        {
            return new ClaudeHealthStatus
            {
                IsInstalled = true,
                IsAuthenticated = false,
                Version = version,
                CliPath = cliPath,
                Error = $"Auth check error: {ex.Message}"
            };
        }
    }

    private static async Task<string?> RunCommandAsync(string fileName, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return output;
    }
}
