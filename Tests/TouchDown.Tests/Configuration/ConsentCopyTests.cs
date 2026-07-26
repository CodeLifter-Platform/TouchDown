using TD.Components.Consent;

namespace TouchDown.Tests.Configuration;

/// <summary>
/// The consent screen is a written statement to whoever runs TouchDown, and the app ships
/// as a release artifact. Diagnostics now include task text, repository paths, branch names,
/// agent output and stack traces — so the screen must not claim otherwise.
///
/// These read the source rather than rendering the component: the point is that the promise
/// in the markup stays true, which is a property of the text itself.
/// </summary>
public class ConsentCopyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TouchDown.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string ConsentMarkup() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), "TouchDown", "Components", "Consent", "TelemetryConsentModal.razor"));

    private static string SettingsMarkup() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), "TouchDown", "Components", "Settings", "TelemetrySettings.razor"));

    [Fact]
    public void ConsentScreen_DoesNotPromiseThatPathsAreNeverCollected()
    {
        // The old copy read "We never collect: Repo names, branch names, or file paths".
        // Keeping that while recording exactly those would make the app assert the opposite
        // of what it does.
        var markup = ConsentMarkup();

        Assert.DoesNotContain("We never collect", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never your code", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsentScreen_DisclosesWhatIsActuallyRecorded()
    {
        var markup = ConsentMarkup();

        Assert.Contains("task description", markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branch", markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stack traces", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsentScreen_SaysNothingLeavesTheMachineByDefault()
    {
        Assert.Contains("Nowhere by default", ConsentMarkup(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsScreen_DoesNotRepeatTheOldPromise()
    {
        var markup = SettingsMarkup();

        Assert.DoesNotContain("No code, paths, task text", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anonymous usage data", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyVersionWasBumped()
    {
        // Anyone who consented under 1.0 agreed to something materially narrower and must
        // be asked again; the modal re-prompts when the stored version differs.
        Assert.NotEqual("1.0", TelemetryConsentModal.TelemetryPolicyVersion);
    }
}
