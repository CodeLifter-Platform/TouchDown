using System.Diagnostics;
using System.Net.Sockets;

namespace TouchDown.Tests.Integration;

/// <summary>
/// Launches the built application as its own process.
///
/// <see cref="StartupTests"/> boots the app in-process via WebApplicationFactory, which is
/// enough for wiring and migrations but cannot catch faults involving Hangfire's static
/// <c>JobStorage.Current</c>: once any host in the test process initializes it, it stays
/// initialized for every later assertion. The Production startup crash this guards against
/// only reproduces in a clean process, so that is what this does.
/// </summary>
public class ProcessStartupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TouchDown.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>The app's build output for the same configuration these tests were built in.</summary>
    private static string? AppDllPath()
    {
        var configuration = Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
        var candidate = Path.Combine(RepoRoot(), "TouchDown", "bin", configuration ?? "Release", "net10.0", "TD.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Application_StartsInAFreshProcess(string environment)
    {
        // The test project references the app project, so its output is always built.
        var dll = AppDllPath();
        Assert.True(dll is not null, "The app binary was not found; expected it beside the test build output.");

        var dbPath = Path.Combine(Path.GetTempPath(), $"td-proc-{Guid.NewGuid():N}.db");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(dll!);
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{FreePort()}";
        psi.Environment["ConnectionStrings__TouchDown"] = $"Data Source={dbPath}";

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            // A healthy app runs until killed; a broken one dies almost immediately.
            using var exitWatch = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var exited = await WaitForExitOrTimeoutAsync(process, TimeSpan.FromSeconds(25), exitWatch.Token);

            if (exited)
            {
                var output = $"{await stdout}\n{await stderr}";
                Assert.Fail(
                    $"The app exited during startup in {environment} with code {process.ExitCode}.\n{output}");
            }
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    /// <summary>True if the process exited within the grace period, false if it is still running.</summary>
    private static async Task<bool> WaitForExitOrTimeoutAsync(Process process, TimeSpan grace, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(grace);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
