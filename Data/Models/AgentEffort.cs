namespace TD.Models;

/// <summary>
/// Reasoning/effort level for a drive's primary model.
/// Maps to the Claude Code CLI <c>--effort</c> flag (low|medium|high|xhigh|max).
/// </summary>
public enum AgentEffort
{
    Low,
    Medium,
    High,
    XHigh,
    Max
}

public static class AgentEffortExtensions
{
    /// <summary>The CLI value accepted by <c>claude --effort</c>.</summary>
    public static string ToCliValue(this AgentEffort effort) => effort switch
    {
        AgentEffort.Low => "low",
        AgentEffort.Medium => "medium",
        AgentEffort.High => "high",
        AgentEffort.XHigh => "xhigh",
        AgentEffort.Max => "max",
        _ => "high"
    };

    public static string ToDisplayName(this AgentEffort effort) => effort switch
    {
        AgentEffort.XHigh => "Extra High",
        _ => effort.ToString()
    };

    /// <summary>
    /// Codex/OpenAI reasoning models only support low|medium|high, so xhigh/max collapse to high.
    /// </summary>
    public static string ToCodexReasoningEffort(this AgentEffort effort) => effort switch
    {
        AgentEffort.Low => "low",
        AgentEffort.Medium => "medium",
        _ => "high"
    };
}
