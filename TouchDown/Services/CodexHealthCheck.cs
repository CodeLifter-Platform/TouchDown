using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TD.Services;

public record CodexHealthStatus
{
    public bool IsInstalled { get; init; }
    public bool IsAuthenticated { get; init; }
    public string? Version { get; init; }
    public string? CliPath { get; init; }
    public string? Error { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    public bool IsHealthy => IsInstalled && IsAuthenticated;
}

public interface ICodexHealthCheck
{
    Task<CodexHealthStatus> CheckAsync(CancellationToken ct = default);
    CodexHealthStatus? LastStatus { get; }
}

public class CodexHealthCheck : ICodexHealthCheck, IHealthCheck
{
    private readonly ILogger<CodexHealthCheck> _logger;
    private CodexHealthStatus? _lastStatus;

    public CodexHealthCheck(ILogger<CodexHealthCheck> logger)
    {
        _logger = logger;
    }

    public CodexHealthStatus? LastStatus => _lastStatus;

    public async Task<CodexHealthStatus> CheckAsync(CancellationToken ct = default)
    {
        var status = await RunCheckAsync(ct);
        _lastStatus = status;
        return status;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var status = await CheckAsync(ct);

        if (status.IsHealthy)
            return HealthCheckResult.Healthy($"Codex CLI v{status.Version} at {status.CliPath}");

        if (status.IsInstalled && !status.IsAuthenticated)
            return HealthCheckResult.Degraded(
                $"Codex CLI is installed but not authenticated. Run 'codex auth' to log in. Error: {status.Error}");

        return HealthCheckResult.Unhealthy(
            $"Codex CLI not found. Install via 'npm i -g @openai/codex'. Error: {status.Error}");
    }

    private async Task<CodexHealthStatus> RunCheckAsync(CancellationToken ct)
    {
        // Step 1: find binary on PATH
        string? cliPath;
        try
        {
            cliPath = (await RunCommandAsync("which", "codex", ct))?.Trim();
            if (string.IsNullOrWhiteSpace(cliPath))
                return new CodexHealthStatus { IsInstalled = false, Error = "'codex' not found on PATH" };
        }
        catch (Exception ex)
        {
            return new CodexHealthStatus { IsInstalled = false, Error = $"Failed to locate codex: {ex.Message}" };
        }

        // Step 2: get version
        string? version;
        try
        {
            version = (await RunCommandAsync("codex", "--version", ct))?.Trim();
        }
        catch (Exception ex)
        {
            return new CodexHealthStatus { IsInstalled = true, CliPath = cliPath, Error = $"Failed to get version: {ex.Message}" };
        }

        // Step 3: verify auth with a minimal exec (30 s timeout)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "codex",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add("--full-auto");
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add("o4-mini");
            psi.ArgumentList.Add("Respond with exactly: ok");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                var errMsg = stderr.Length > 300 ? stderr[..300] : stderr;
                _logger.LogWarning("Codex auth check failed (exit {Code}): {Error}", process.ExitCode, errMsg);
                return new CodexHealthStatus
                {
                    IsInstalled = true,
                    IsAuthenticated = false,
                    Version = version,
                    CliPath = cliPath,
                    Error = $"Auth check failed (exit {process.ExitCode}): {errMsg}"
                };
            }

            _logger.LogInformation("Codex health check passed: v{Version} at {Path}", version, cliPath);
            return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = true, Version = version, CliPath = cliPath };
        }
        catch (OperationCanceledException)
        {
            return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = false, Version = version, CliPath = cliPath, Error = "Auth check timed out after 45s" };
        }
        catch (Exception ex)
        {
            return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = false, Version = version, CliPath = cliPath, Error = $"Auth check error: {ex.Message}" };
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

