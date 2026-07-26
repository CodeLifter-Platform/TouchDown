using System.Diagnostics;

namespace TouchDown.Tests.TestSupport;

/// <summary>
/// A throwaway directory, optionally initialized as a git repo with a deterministic
/// identity so commits work without depending on the machine's git config.
/// </summary>
public sealed class TempRepo : IDisposable
{
    public string Path { get; }

    private TempRepo(string path) => Path = path;

    public static TempRepo CreateEmptyDirectory()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "td-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(path);
        return new TempRepo(path);
    }

    public static TempRepo CreateGitRepo()
    {
        var repo = CreateEmptyDirectory();
        Git(repo.Path, "init", "-b", "main");
        Git(repo.Path, "config", "user.email", "tests@touchdown.local");
        Git(repo.Path, "config", "user.name", "TouchDown Tests");
        // Keep signing off — a machine with commit.gpgsign=true would otherwise hang.
        Git(repo.Path, "config", "commit.gpgsign", "false");
        return repo;
    }

    public TempRepo WithCommit(string fileName = "README.md", string content = "initial")
    {
        WriteFile(fileName, content);
        Git(Path, "add", "-A");
        Git(Path, "commit", "-m", "initial commit");
        return this;
    }

    public void WriteFile(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>Runs git and returns stdout, throwing with stderr attached on failure.</summary>
    public static string Git(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdout, stderr);
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr.Result}");

        return stdout.Result;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException) { /* best effort — a temp dir left behind must not fail a test */ }
        catch (UnauthorizedAccessException) { }
    }
}
