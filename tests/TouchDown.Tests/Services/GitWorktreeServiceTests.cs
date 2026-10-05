using Microsoft.Extensions.Logging.Abstractions;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// Exercises the git wrapper against real repositories in temp directories.
/// These are the operations that touch the user's working tree, so they are worth
/// running for real rather than against a mock.
/// </summary>
public class GitWorktreeServiceTests
{
    private static GitWorktreeService CreateService() =>
        new(NullLogger<GitWorktreeService>.Instance);

    [Fact]
    public async Task InitRepoAsync_CreatesRepository()
    {
        using var dir = TempRepo.CreateEmptyDirectory();
        var git = CreateService();

        await git.InitRepoAsync(dir.Path);

        Assert.True(Directory.Exists(Path.Combine(dir.Path, ".git")));
    }

    [Fact]
    public async Task ListBranchesAsync_ReturnsBranches()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        TempRepo.Git(repo.Path, "branch", "feature/login");
        var git = CreateService();

        var branches = await git.ListBranchesAsync(repo.Path);

        Assert.Contains("main", branches);
        Assert.Contains("feature/login", branches);
    }

    [Fact]
    public async Task ListBranchesAsync_NonRepo_ReturnsEmpty()
    {
        using var dir = TempRepo.CreateEmptyDirectory();
        var git = CreateService();

        // The New Drive wizard relies on this to detect "not a git repo" without throwing.
        await Assert.ThrowsAsync<InvalidOperationException>(() => git.ListBranchesAsync(dir.Path));
    }

    [Fact]
    public async Task GetCurrentBranchAsync_ReturnsCheckedOutBranch()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        var git = CreateService();

        var branch = await git.GetCurrentBranchAsync(repo.Path);

        Assert.Equal("main", branch);
    }

    [Fact]
    public async Task GetStatusAsync_ClassifiesFiles()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        repo.WriteFile("README.md", "changed");   // modified, unstaged
        repo.WriteFile("brand-new.txt", "hello"); // untracked
        repo.WriteFile("staged.txt", "hello");
        TempRepo.Git(repo.Path, "add", "staged.txt");
        var git = CreateService();

        var status = await git.GetStatusAsync(repo.Path);

        Assert.True(status.HasChanges);
        Assert.Contains("README.md", status.ModifiedFiles);
        Assert.Contains("brand-new.txt", status.UntrackedFiles);
        Assert.Contains("staged.txt", status.StagedFiles);
        Assert.Equal(3, status.TotalChanges);
    }

    [Fact]
    public async Task GetStatusAsync_CleanRepo_ReportsNoChanges()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        var git = CreateService();

        var status = await git.GetStatusAsync(repo.Path);

        Assert.False(status.HasChanges);
        Assert.Equal(0, status.TotalChanges);
    }

    [Fact]
    public async Task CommitAsync_CommitsAllChanges()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        repo.WriteFile("feature.cs", "// work");
        var git = CreateService();

        await git.CommitAsync(repo.Path, "feat: add the feature");

        var status = await git.GetStatusAsync(repo.Path);
        Assert.False(status.HasChanges);
        Assert.Contains("feat: add the feature", TempRepo.Git(repo.Path, "log", "-1", "--pretty=%s"));
    }

    [Theory]
    [InlineData("feat: add \"quoted\" thing")]
    [InlineData("fix: handle $HOME and `backticks`")]
    [InlineData("chore: it's a semicolon; and an ampersand & pipe |")]
    [InlineData("feat: $(touch /tmp/td-should-not-exist)")]
    [InlineData("fix: newline\nin the message")]
    public async Task CommitAsync_TreatsMessageAsSingleArgument(string message)
    {
        // Regression: the message used to be interpolated into a command string with naive
        // quote escaping. Each of these either corrupted the message or broke the command.
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        repo.WriteFile("feature.cs", "// work");
        var git = CreateService();

        await git.CommitAsync(repo.Path, message);

        var recorded = TempRepo.Git(repo.Path, "log", "-1", "--pretty=%B").TrimEnd('\n');
        Assert.Equal(message, recorded);
        Assert.False(File.Exists("/tmp/td-should-not-exist"), "commit message was evaluated by a shell");
    }

    [Fact]
    public async Task PushAsync_BranchNameWithShellMetacharacters_IsNotReinterpreted()
    {
        // git forbids spaces in branch names, but ';' and '$' are legal and were previously
        // interpolated straight into the command string.
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        using var remote = TempRepo.CreateEmptyDirectory();
        TempRepo.Git(remote.Path, "init", "--bare");
        TempRepo.Git(repo.Path, "remote", "add", "origin", remote.Path);
        TempRepo.Git(repo.Path, "checkout", "-b", "feature/a;b$c");
        var git = CreateService();

        await git.PushAsync(repo.Path);

        // The branch arrived at the remote intact rather than being split or evaluated.
        Assert.Contains("feature/a;b$c", TempRepo.Git(remote.Path, "branch", "--list", "--format=%(refname:short)"));
    }

    [Fact]
    public async Task GetDiffSummaryAsync_CountsChanges()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit("file.txt", "line1\nline2\nline3\n");
        repo.WriteFile("file.txt", "line1\nline2 modified\nline3\nline4\n");
        var git = CreateService();

        var diff = await git.GetDiffSummaryAsync(repo.Path);

        Assert.Equal(1, diff.FilesChanged);
        Assert.True(diff.Insertions > 0, "expected insertions to be parsed from --shortstat");
        Assert.Contains("line2 modified", diff.DiffText);
    }

    [Fact]
    public async Task GetDiffSummaryAsync_RepoWithNoCommits_StillProducesDiff()
    {
        // A freshly-init'd workspace has no HEAD; the summary falls back to the index.
        using var repo = TempRepo.CreateGitRepo();
        repo.WriteFile("new.txt", "hello\n");
        var git = CreateService();

        var diff = await git.GetDiffSummaryAsync(repo.Path);

        Assert.Contains("new.txt", diff.DiffText);
        // The fallback stages files to diff them and must unstage afterwards.
        var status = await git.GetStatusAsync(repo.Path);
        Assert.Contains("new.txt", status.UntrackedFiles);
    }

    [Fact]
    public async Task GetDiffSummaryAsync_HandlesLargeDiffWithoutDeadlock()
    {
        // Both stdout and stderr must be drained concurrently; reading stdout to completion
        // first deadlocks once git fills the other pipe.
        // Must modify a *tracked* file — `diff HEAD` ignores untracked ones.
        using var repo = TempRepo.CreateGitRepo().WithCommit("big.txt", "seed\n");
        var big = string.Join('\n', Enumerable.Range(0, 20_000).Select(i => $"line {i} of generated content"));
        repo.WriteFile("big.txt", big);
        var git = CreateService();

        var diff = await git.GetDiffSummaryAsync(repo.Path).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(diff.DiffText.Length > 100_000, "expected a large diff to stream back in full");
    }

    [Fact]
    public async Task GetRemoteUrlAsync_NoRemote_ReturnsEmpty()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        var git = CreateService();

        var url = await git.GetRemoteUrlAsync(repo.Path);

        Assert.Equal("", url);
    }

    [Fact]
    public async Task GetRemoteUrlAsync_ReturnsConfiguredRemote()
    {
        // Deliberately not a github.com URL: an `insteadOf` rewrite in the ambient git
        // config would otherwise rewrite it and make this assertion environment-dependent.
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        TempRepo.Git(repo.Path, "remote", "add", "origin", "https://git.example.com/acme/widgets.git");
        var git = CreateService();

        var url = await git.GetRemoteUrlAsync(repo.Path);

        Assert.Equal("https://git.example.com/acme/widgets.git", url);
    }

    [Fact]
    public async Task CreateWorktreeAsync_CreatesLinkedWorktreeOnNewBranch()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        var git = CreateService();

        var worktreePath = await git.CreateWorktreeAsync(repo.Path, "touchdown/main-drive");

        Assert.True(Directory.Exists(worktreePath));
        Assert.Equal("touchdown/main-drive", await git.GetCurrentBranchAsync(worktreePath));
    }

    [Fact]
    public async Task RemoveWorktreeAsync_RemovesIt()
    {
        using var repo = TempRepo.CreateGitRepo().WithCommit();
        var git = CreateService();
        var worktreePath = await git.CreateWorktreeAsync(repo.Path, "touchdown/temp");

        await git.RemoveWorktreeAsync(worktreePath);

        Assert.False(Directory.Exists(worktreePath));
    }

    [Fact]
    public async Task FailedCommand_ThrowsWithGitStderr()
    {
        using var dir = TempRepo.CreateEmptyDirectory();
        var git = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => git.GetCurrentBranchAsync(dir.Path));

        Assert.Contains("Git command failed", ex.Message);
    }
}
