using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// Provider availability is probed on every New Drive page load, and each probe used to
/// shell out to the CLI. For Codex that meant a billed model call per probe. Results are
/// now cached for a short window; these tests pin that behaviour.
///
/// The checks run against whatever CLIs exist on the test machine — usually none — so the
/// assertions are about caching, not about the health verdict itself.
/// </summary>
public class HealthCheckCachingTests
{
    [Fact]
    public async Task Claude_RepeatedChecksWithinWindow_ReuseTheSameResult()
    {
        var time = new FakeTimeProvider();
        var check = new ClaudeHealthCheck(NullLogger<ClaudeHealthCheck>.Instance, time);

        var first = await check.CheckAsync();
        time.Advance(ClaudeHealthCheck.CacheDuration - TimeSpan.FromSeconds(1));
        var second = await check.CheckAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Claude_AfterWindowExpires_RunsTheCheckAgain()
    {
        var time = new FakeTimeProvider();
        var check = new ClaudeHealthCheck(NullLogger<ClaudeHealthCheck>.Instance, time);

        var first = await check.CheckAsync();
        time.Advance(ClaudeHealthCheck.CacheDuration + TimeSpan.FromSeconds(1));
        var second = await check.CheckAsync();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Codex_RepeatedChecksWithinWindow_ReuseTheSameResult()
    {
        var time = new FakeTimeProvider();
        var check = new CodexHealthCheck(NullLogger<CodexHealthCheck>.Instance, time);

        var first = await check.CheckAsync();
        time.Advance(ClaudeHealthCheck.CacheDuration - TimeSpan.FromSeconds(1));
        var second = await check.CheckAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Codex_AfterWindowExpires_RunsTheCheckAgain()
    {
        var time = new FakeTimeProvider();
        var check = new CodexHealthCheck(NullLogger<CodexHealthCheck>.Instance, time);

        var first = await check.CheckAsync();
        time.Advance(CodexHealthCheck.CacheDuration + TimeSpan.FromSeconds(1));
        var second = await check.CheckAsync();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ConcurrentChecks_OnlyProduceOneResult()
    {
        // Several circuits loading the page at once must not each shell out.
        var time = new FakeTimeProvider();
        var check = new CodexHealthCheck(NullLogger<CodexHealthCheck>.Instance, time);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => check.CheckAsync()));

        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public async Task LastStatus_IsPopulatedAfterACheck()
    {
        var check = new ClaudeHealthCheck(NullLogger<ClaudeHealthCheck>.Instance, new FakeTimeProvider());
        Assert.Null(check.LastStatus);

        var status = await check.CheckAsync();

        Assert.Same(status, check.LastStatus);
    }

    // ── Codex CLI compatibility detection ───────────────────────────────────

    [Theory]
    [InlineData("error: unrecognized subcommand 'status'")]
    [InlineData("Unknown command: login")]
    [InlineData("error: unexpected argument 'status' found")]
    [InlineData("invalid subcommand")]
    public void UnknownCommandOutput_IsDetected(string stderr)
    {
        // An older CLI without `login status` must fall back to the credential file
        // rather than being reported as "not authenticated".
        Assert.True(CodexHealthCheck.LooksLikeUnknownCommand(stderr, ""));
    }

    [Theory]
    [InlineData("Not logged in. Run `codex login`.")]
    [InlineData("")]
    [InlineData("authentication expired")]
    public void GenuineAuthFailure_IsNotMistakenForAnUnknownCommand(string stderr)
    {
        Assert.False(CodexHealthCheck.LooksLikeUnknownCommand(stderr, ""));
    }

    [Fact]
    public void UnknownCommandOnStdout_IsAlsoDetected()
    {
        Assert.True(CodexHealthCheck.LooksLikeUnknownCommand("", "unrecognized subcommand"));
    }
}
