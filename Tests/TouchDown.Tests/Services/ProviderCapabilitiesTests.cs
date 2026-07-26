using TD.Models;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// Effort flags and model ids are provider-specific. The huddle and the orchestrator both
/// route through here, so a regression would silently send a Claude model id to Codex.
/// </summary>
public class ProviderCapabilitiesTests
{
    // ── Effort ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AgentEffort.Low, "low")]
    [InlineData(AgentEffort.Medium, "medium")]
    [InlineData(AgentEffort.High, "high")]
    [InlineData(AgentEffort.XHigh, "xhigh")]
    [InlineData(AgentEffort.Max, "max")]
    public void ResolveEffort_Claude_PassesEffortThrough(AgentEffort effort, string expected)
    {
        var result = ProviderCapabilities.ResolveEffort("claude-code", "claude-opus-4-8", effort);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(AgentEffort.Low, "low")]
    [InlineData(AgentEffort.Medium, "medium")]
    [InlineData(AgentEffort.High, "high")]
    [InlineData(AgentEffort.XHigh, "high")]
    [InlineData(AgentEffort.Max, "high")]
    public void ResolveEffort_Codex_CollapsesAboveHigh(AgentEffort effort, string expected)
    {
        // Codex reasoning models only accept low|medium|high.
        var result = ProviderCapabilities.ResolveEffort("codex", "gpt-5.4", effort);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveEffort_ClaudeHaiku_ReturnsNull()
    {
        // The --effort flag is rejected by Haiku; sending one fails the run.
        var result = ProviderCapabilities.ResolveEffort("claude-code", "claude-haiku-4-5", AgentEffort.High);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEffort_CodexIsCaseInsensitive()
    {
        Assert.Equal("high", ProviderCapabilities.ResolveEffort("CODEX", "gpt-5.4", AgentEffort.Max));
    }

    [Fact]
    public void ResolveEffort_NullProvider_TreatedAsClaude()
    {
        Assert.Equal("xhigh", ProviderCapabilities.ResolveEffort(null, "claude-opus-4-8", AgentEffort.XHigh));
        Assert.Null(ProviderCapabilities.ResolveEffort(null, "claude-haiku-4-5", AgentEffort.XHigh));
    }

    [Fact]
    public void ResolveEffort_CodexHaiku_StillReturnsEffort()
    {
        // The Haiku carve-out is Claude-specific and must not leak into other providers.
        var result = ProviderCapabilities.ResolveEffort("codex", "some-haiku-named-model", AgentEffort.Low);

        Assert.Equal("low", result);
    }

    // ── Model ───────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveModel_NonClaudeProvider_UsesDriveModel()
    {
        // A member's model is a ClaudeModel; sending it to Codex would be an unknown model.
        var result = ProviderCapabilities.ResolveModel(
            providerId: "codex",
            memberModelId: "claude-sonnet-4-6",
            driveModelId: "gpt-5.4",
            preferDriveModel: false);

        Assert.Equal("gpt-5.4", result);
    }

    [Fact]
    public void ResolveModel_NonClaudeProvider_FallsBackToMemberModelWhenDriveModelMissing()
    {
        var result = ProviderCapabilities.ResolveModel("codex", "claude-sonnet-4-6", null, false);

        Assert.Equal("claude-sonnet-4-6", result);
    }

    [Fact]
    public void ResolveModel_Claude_KeepsMemberModelForNonLeaders()
    {
        // Squad members keep their own configured model unless the drive overrides the leader.
        var result = ProviderCapabilities.ResolveModel(
            "claude-code", "claude-sonnet-4-6", "claude-opus-4-8", preferDriveModel: false);

        Assert.Equal("claude-sonnet-4-6", result);
    }

    [Fact]
    public void ResolveModel_Claude_LeaderPrefersDriveModel()
    {
        var result = ProviderCapabilities.ResolveModel(
            "claude-code", "claude-sonnet-4-6", "claude-opus-4-8", preferDriveModel: true);

        Assert.Equal("claude-opus-4-8", result);
    }

    [Fact]
    public void ResolveModel_Claude_LeaderFallsBackWhenNoDriveModel()
    {
        var result = ProviderCapabilities.ResolveModel("claude-code", "claude-sonnet-4-6", null, true);

        Assert.Equal("claude-sonnet-4-6", result);
    }

    [Fact]
    public void ResolveModel_NullProvider_TreatedAsClaude()
    {
        var result = ProviderCapabilities.ResolveModel(null, "claude-sonnet-4-6", "claude-opus-4-8", false);

        Assert.Equal("claude-sonnet-4-6", result);
    }
}
