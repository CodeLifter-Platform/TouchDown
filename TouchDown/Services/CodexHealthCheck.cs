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
    /// <summary>
    /// How long a check result stays fresh. Provider availability is probed on every
    /// New Drive page load, so without this the CLI gets shelled out to repeatedly.
    /// </summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly ILogger<CodexHealthCheck> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time;
    private CodexHealthStatus? _lastStatus;
    private DateTimeOffset _lastCheckedAt;

    public CodexHealthCheck(ILogger<CodexHealthCheck> logger, TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public CodexHealthStatus? LastStatus => _lastStatus;

    public async Task<CodexHealthStatus> CheckAsync(CancellationToken ct = default)
    {
        if (TryGetFresh(out var cached)) return cached;

        await _gate.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while this one waited for the gate.
            if (TryGetFresh(out cached)) return cached;

            var status = await RunCheckAsync(ct);
            _lastStatus = status;
            _lastCheckedAt = _time.GetUtcNow();
            return status;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetFresh(out CodexHealthStatus status)
    {
        var last = _lastStatus;
        if (last != null && _time.GetUtcNow() - _lastCheckedAt < CacheDuration)
        {
            status = last;
            return true;
        }

        status = null!;
        return false;
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

        // Step 3: verify auth via `codex login status`.
        // This used to run a real `codex exec` prompt, which billed a model call on every
        // check — and the check runs on startup AND on every provider-availability probe.
        // `login status` answers the same question without inference.
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
            psi.ArgumentList.Add("login");
            psi.ArgumentList.Add("status");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Codex health check passed: v{Version} at {Path}", version, cliPath);
                return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = true, Version = version, CliPath = cliPath };
            }

            // An older CLI without `login status` reports an unknown-command error rather than
            // "not logged in". Fall back to the credential file so those installs still work.
            if (LooksLikeUnknownCommand(stderr, stdout))
            {
                var hasCredentials = CodexCredentialsExist();
                _logger.LogInformation(
                    "'codex login status' unsupported by this CLI; fell back to credential file (found: {Found})",
                    hasCredentials);

                return new CodexHealthStatus
                {
                    IsInstalled = true,
                    IsAuthenticated = hasCredentials,
                    Version = version,
                    CliPath = cliPath,
                    Error = hasCredentials ? null : "No Codex credentials found. Run 'codex login'."
                };
            }

            var errMsg = Truncate(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            _logger.LogWarning("Codex auth check failed (exit {Code}): {Error}", process.ExitCode, errMsg);
            return new CodexHealthStatus
            {
                IsInstalled = true,
                IsAuthenticated = false,
                Version = version,
                CliPath = cliPath,
                Error = $"Not logged in (exit {process.ExitCode}): {errMsg}"
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = false, Version = version, CliPath = cliPath, Error = "Auth check timed out after 15s" };
        }
        catch (Exception ex)
        {
            return new CodexHealthStatus { IsInstalled = true, IsAuthenticated = false, Version = version, CliPath = cliPath, Error = $"Auth check error: {ex.Message}" };
        }
    }

    internal static bool LooksLikeUnknownCommand(string? stderr, string? stdout)
    {
        var text = $"{stderr}\n{stdout}";
        return text.Contains("unrecognized subcommand", StringComparison.OrdinalIgnoreCase)
               || text.Contains("unknown command", StringComparison.OrdinalIgnoreCase)
               || text.Contains("unexpected argument", StringComparison.OrdinalIgnoreCase)
               || text.Contains("invalid subcommand", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CodexCredentialsExist()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return false;
        return File.Exists(Path.Combine(home, ".codex", "auth.json"));
    }

    private static string Truncate(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Length > 300 ? text[..300] : text;

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

