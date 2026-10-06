using Microsoft.Extensions.Logging.Abstractions;
using TD.Areas.Drives.New;
using TD.Models;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// The huddle streams through whichever provider the drive selected. A Codex drive used to
/// plan on Claude regardless; now the selection is honoured, an unknown id falls back to
/// Claude Code instead of failing, and a provider error reaches the screen as text.
/// </summary>
public class DrivesNewServiceTests
{
    private static (DrivesNewService Service, FakeAgentProvider Claude, FakeAgentProvider Codex) Create(TestDb db)
    {
        var claude = new FakeAgentProvider("claude says hi", providerId: "claude-code");
        var codex = new FakeAgentProvider("codex says hi", providerId: "codex");
        var registry = new AgentProviderRegistry([claude, codex], NullLogger<AgentProviderRegistry>.Instance);
        var service = new DrivesNewService(new DrivesNewServiceDA(db), orchestration: null!, claude: null!, registry);
        return (service, claude, codex);
    }

    private static async Task<string> CollectAsync(IAsyncEnumerable<string> stream)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in stream) sb.Append(chunk);
        return sb.ToString();
    }

    [Fact]
    public async Task The_selected_provider_is_the_one_that_streams()
    {
        using var db = TestDb.Create();
        var (service, claude, codex) = Create(db);

        var text = await CollectAsync(service.StreamQbResponseAsync("codex", "gpt-5.4", "sys", "plan it", null, "high"));

        Assert.Equal("codex says hi ", text);
        Assert.Empty(claude.Calls);
        var call = Assert.Single(codex.Calls);
        Assert.Equal("gpt-5.4", call.ModelId);
        Assert.Equal("high", call.Effort);
        Assert.Equal(AgentDefaults.BlockedSubagentTools, call.DisallowedTools);
    }

    [Fact]
    public async Task An_unknown_provider_id_falls_back_to_claude_code()
    {
        using var db = TestDb.Create();
        var (service, claude, codex) = Create(db);

        var text = await CollectAsync(service.StreamQbResponseAsync("gemini", "m", "sys", "plan it", null));

        Assert.Equal("claude says hi ", text);
        Assert.Single(claude.Calls);
        Assert.Empty(codex.Calls);
    }

    [Fact]
    public async Task No_provider_id_means_claude_code()
    {
        using var db = TestDb.Create();
        var (service, claude, _) = Create(db);

        await CollectAsync(service.StreamQbResponseAsync(null, "m", "sys", "plan it", null));

        Assert.Single(claude.Calls);
    }

    [Fact]
    public async Task Research_runs_with_read_only_web_tools_and_no_permission_prompts()
    {
        using var db = TestDb.Create();
        var (service, claude, _) = Create(db);

        await CollectAsync(service.StreamCoordinatorResearchAsync("claude-code", "m", "sys", "look it up", "/tmp/repo"));

        var call = Assert.Single(claude.Calls);
        Assert.True(call.DangerouslySkipPermissions);
        Assert.Equal(["WebSearch", "WebFetch", "Read", "Glob", "Grep"], call.AllowedTools);
        Assert.Equal("/tmp/repo", call.WorkingDirectory);
    }

    [Fact]
    public async Task A_provider_error_is_surfaced_in_the_stream_rather_than_swallowed()
    {
        using var db = TestDb.Create();
        var failing = new FakeAgentProvider(providerId: "claude-code") { ErrorResult = "claude exited with code 1: not logged in" };
        var registry = new AgentProviderRegistry([failing], NullLogger<AgentProviderRegistry>.Instance);
        var service = new DrivesNewService(new DrivesNewServiceDA(db), orchestration: null!, claude: null!, registry);

        var text = await CollectAsync(service.StreamQbResponseAsync("claude-code", "m", "sys", "plan it", null));

        Assert.Contains("not logged in", text);
    }

    [Fact]
    public async Task Only_available_providers_are_offered_to_the_wizard()
    {
        using var db = TestDb.Create();
        var (service, _, codex) = Create(db);
        codex.IsAvailable = false;

        var offered = await service.GetAvailableProvidersAsync();

        var only = Assert.Single(offered);
        Assert.Equal("claude-code", only.ProviderId);
        Assert.NotEmpty(only.Models);
    }
}
