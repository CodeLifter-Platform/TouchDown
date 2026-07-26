using TD.Models;

namespace TD.Services;

/// <summary>
/// Provider-specific quirks that both the huddle and the drive orchestrator need to agree on.
/// Kept in one place so planning and execution can't drift apart on effort flags or model ids.
/// </summary>
public static class ProviderCapabilities
{
    public const string ClaudeCodeProviderId = "claude-code";
    public const string CodexProviderId = "codex";

    /// <summary>
    /// Maps an effort level to the CLI value for a given provider and model,
    /// or null when the combination does not accept an effort flag.
    /// </summary>
    public static string? ResolveEffort(string? providerId, string modelId, AgentEffort effort)
    {
        if (string.Equals(providerId, CodexProviderId, StringComparison.OrdinalIgnoreCase))
            return effort.ToCodexReasoningEffort();

        // Claude: the --effort flag is not supported on Haiku.
        if (modelId.Contains("haiku", StringComparison.OrdinalIgnoreCase))
            return null;

        return effort.ToCliValue();
    }

    /// <summary>
    /// Picks the model a team member should run on for a given provider.
    ///
    /// <see cref="AgentMember.Model"/> is a <see cref="ClaudeModel"/>, so it only means
    /// something on the Claude provider. On any other provider the member's model id would
    /// not be recognised, so the drive's selected model is used instead.
    /// </summary>
    /// <param name="providerId">The drive's provider, or null for the Claude default.</param>
    /// <param name="memberModelId">The member's own model id (a Claude model).</param>
    /// <param name="driveModelId">The model chosen for the drive on the current provider.</param>
    /// <param name="preferDriveModel">
    /// True for the leader, whose configured model the drive-level selection overrides.
    /// </param>
    public static string ResolveModel(
        string? providerId,
        string memberModelId,
        string? driveModelId,
        bool preferDriveModel)
    {
        var isClaude = string.IsNullOrEmpty(providerId)
                       || string.Equals(providerId, ClaudeCodeProviderId, StringComparison.OrdinalIgnoreCase);

        if (!isClaude)
            return !string.IsNullOrEmpty(driveModelId) ? driveModelId : memberModelId;

        return preferDriveModel && !string.IsNullOrEmpty(driveModelId) ? driveModelId : memberModelId;
    }
}
