using System.ComponentModel.DataAnnotations;

namespace TD.Models;

/// <summary>Which part of the drive a conversation turn belongs to.</summary>
public enum TurnPhase
{
    /// <summary>The Head Coach ↔ Quarterback planning conversation in the Huddle.</summary>
    Huddle,

    /// <summary>The Quarterback turning the huddle into a structured plan (stored as a "comment").</summary>
    Planning,

    /// <summary>An agent's prompt/response while executing a Play.</summary>
    Execution
}

/// <summary>
/// A single message in a Drive's conversation — one row per turn, written as it happens.
/// Huddle turns capture the planning chat; Execution turns capture each agent's prompt and reply.
/// Ordered by <see cref="Timestamp"/> then <see cref="Id"/> (insertion order).
/// </summary>
public class DriveTurn
{
    public int Id { get; set; }

    public int DriveId { get; set; }
    public Drive? Drive { get; set; }

    /// <summary>The Play this turn belongs to, for Execution turns (null for Huddle turns).</summary>
    public int? PlayId { get; set; }

    public TurnPhase Phase { get; set; } = TurnPhase.Huddle;

    /// <summary>Semantic role: "user" (Coach / prompt), "assistant" (agent reply), or "comment" (QB plan annotation).</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    /// <summary>Display name of the speaker, e.g. "Head Coach", "The Quarterback", "Left Guard".</summary>
    [MaxLength(100)]
    public string? AgentName { get; set; }

    [Required, MaxLength(200000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>JSON array of tool names used (Execution turns only).</summary>
    [MaxLength(2000)]
    public string? ToolsUsed { get; set; }

    public double? CostUsd { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
