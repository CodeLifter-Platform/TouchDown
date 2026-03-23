using System.Diagnostics;

namespace TouchDown.Services;

public interface IGitWorktreeService
{
    Task<string> CreateWorktreeAsync(string repoPath, string branchName, CancellationToken ct = default);
    Task RemoveWorktreeAsync(string worktreePath, CancellationToken ct = default);
    Task<List<string>> ListBranchesAsync(string repoPath, CancellationToken ct = default);
    Task<string> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default);
    Task<string> CloneRepoAsync(string repoUrl, string targetPath, CancellationToken ct = default);
}

public class GitWorktreeService : IGitWorktreeService
{
    private readonly ILogger<GitWorktreeService> _logger;

    public GitWorktreeService(ILogger<GitWorktreeService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateWorktreeAsync(string repoPath, string branchName, CancellationToken ct = default)
    {
        var worktreePath = Path.Combine(repoPath, ".touchdown", "worktrees", branchName);
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);

        var result = await RunGitAsync(repoPath, $"worktree add \"{worktreePath}\" -b touchdown/{branchName}", ct);
        _logger.LogInformation("Created worktree at {Path} on branch touchdown/{Branch}", worktreePath, branchName);
        return worktreePath;
    }

    public async Task RemoveWorktreeAsync(string worktreePath, CancellationToken ct = default)
    {
        var parentRepo = await RunGitAsync(worktreePath, "rev-parse --git-common-dir", ct);
        var repoPath = Path.GetDirectoryName(parentRepo.Trim())!;
        await RunGitAsync(repoPath, $"worktree remove \"{worktreePath}\" --force", ct);
        _logger.LogInformation("Removed worktree at {Path}", worktreePath);
    }

    public async Task<List<string>> ListBranchesAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "branch --list --format=%(refname:short)", ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public async Task<string> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD", ct);
        return output.Trim();
    }

    public async Task<string> CloneRepoAsync(string repoUrl, string targetPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetPath);
        await RunGitAsync(targetPath, $"clone \"{repoUrl}\" .", ct);
        _logger.LogInformation("Cloned {Repo} to {Path}", repoUrl, targetPath);
        return targetPath;
    }

    private async Task<string> RunGitAsync(string workingDir, string arguments, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Git command failed: git {Args}\n{Error}", arguments, error);
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return output;
    }
}
