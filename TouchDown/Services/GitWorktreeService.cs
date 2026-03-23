using System.Diagnostics;

namespace TD.Services;

public record GitStatusResult
{
    public List<string> StagedFiles { get; init; } = [];
    public List<string> ModifiedFiles { get; init; } = [];
    public List<string> UntrackedFiles { get; init; } = [];
    public bool HasChanges => StagedFiles.Count > 0 || ModifiedFiles.Count > 0 || UntrackedFiles.Count > 0;
    public int TotalChanges => StagedFiles.Count + ModifiedFiles.Count + UntrackedFiles.Count;
}

public record GitDiffSummary
{
    public string DiffText { get; init; } = "";
    public int FilesChanged { get; init; }
    public int Insertions { get; init; }
    public int Deletions { get; init; }
}

public interface IGitWorktreeService
{
    Task<string> CreateWorktreeAsync(string repoPath, string branchName, CancellationToken ct = default);
    Task RemoveWorktreeAsync(string worktreePath, CancellationToken ct = default);
    Task<List<string>> ListBranchesAsync(string repoPath, CancellationToken ct = default);
    Task<string> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default);
    Task<string> CloneRepoAsync(string repoUrl, string targetPath, CancellationToken ct = default);
    Task<GitStatusResult> GetStatusAsync(string workingDir, CancellationToken ct = default);
    Task<GitDiffSummary> GetDiffSummaryAsync(string workingDir, CancellationToken ct = default);
    Task StageAllAsync(string workingDir, CancellationToken ct = default);
    Task<string> CommitAsync(string workingDir, string message, CancellationToken ct = default);
    Task PushAsync(string workingDir, string? remoteBranch = null, CancellationToken ct = default);
    Task<string> GetRemoteUrlAsync(string workingDir, CancellationToken ct = default);
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
        var safeBranch = branchName.Replace("/", "-");
        var worktreePath = Path.Combine(repoPath, ".touchdown", "worktrees", safeBranch);
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);

        await RunGitAsync(repoPath, $"worktree add \"{worktreePath}\" -b {branchName}", ct);
        _logger.LogInformation("Created worktree at {Path} on branch {Branch}", worktreePath, branchName);
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

    public async Task<GitStatusResult> GetStatusAsync(string workingDir, CancellationToken ct = default)
    {
        var output = await RunGitAsync(workingDir, "status --porcelain", ct);
        var staged = new List<string>();
        var modified = new List<string>();
        var untracked = new List<string>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var x = line[0]; // staging area
            var y = line[1]; // working tree
            var file = line[3..].Trim();

            if (x == '?') untracked.Add(file);
            else if (x != ' ') staged.Add(file);
            else if (y != ' ') modified.Add(file);
        }

        return new GitStatusResult
        {
            StagedFiles = staged,
            ModifiedFiles = modified,
            UntrackedFiles = untracked,
        };
    }

    public async Task<GitDiffSummary> GetDiffSummaryAsync(string workingDir, CancellationToken ct = default)
    {
        // Get diff of staged + unstaged combined
        var diffText = await RunGitAsync(workingDir, "diff HEAD", ct);
        var statLine = await RunGitAsync(workingDir, "diff HEAD --shortstat", ct);

        int files = 0, insertions = 0, deletions = 0;
        var stat = statLine.Trim();
        if (!string.IsNullOrEmpty(stat))
        {
            // "3 files changed, 45 insertions(+), 12 deletions(-)"
            var parts = stat.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("file")) int.TryParse(trimmed.Split(' ')[0], out files);
                else if (trimmed.Contains("insertion")) int.TryParse(trimmed.Split(' ')[0], out insertions);
                else if (trimmed.Contains("deletion")) int.TryParse(trimmed.Split(' ')[0], out deletions);
            }
        }

        return new GitDiffSummary
        {
            DiffText = diffText,
            FilesChanged = files,
            Insertions = insertions,
            Deletions = deletions,
        };
    }

    public async Task StageAllAsync(string workingDir, CancellationToken ct = default)
    {
        await RunGitAsync(workingDir, "add -A", ct);
    }

    public async Task<string> CommitAsync(string workingDir, string message, CancellationToken ct = default)
    {
        await StageAllAsync(workingDir, ct);
        var output = await RunGitAsync(workingDir, $"commit -m \"{message.Replace("\"", "\\\"")}\"", ct);
        _logger.LogInformation("Committed in {Dir}: {Message}", workingDir, message);
        return output.Trim();
    }

    public async Task PushAsync(string workingDir, string? remoteBranch = null, CancellationToken ct = default)
    {
        var branch = remoteBranch ?? await GetCurrentBranchAsync(workingDir, ct);
        await RunGitAsync(workingDir, $"push -u origin {branch}", ct);
        _logger.LogInformation("Pushed {Branch} to origin from {Dir}", branch, workingDir);
    }

    public async Task<string> GetRemoteUrlAsync(string workingDir, CancellationToken ct = default)
    {
        try
        {
            var output = await RunGitAsync(workingDir, "remote get-url origin", ct);
            return output.Trim();
        }
        catch
        {
            return "";
        }
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
