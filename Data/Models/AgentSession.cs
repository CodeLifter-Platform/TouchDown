namespace TD.Models;

public class AgentSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public Drive Drive { get; set; } = null!;
    public AgentTeam Team { get; set; } = null!;
    public int CurrentStep { get; set; }
    public SourceType SourceType { get; set; }
    public string? RepoPath { get; set; }
    public string? Branch { get; set; }
    public WorkspaceMode WorkspaceMode { get; set; }
    public string? FreshFolderName { get; set; }
    public string? PrBranchName { get; set; }
    public string? TaskDescription { get; set; }
    public string? Name { get; set; }
    public int MaxParallelism { get; set; } = 2;

    /// <summary>The selected agent provider ID (e.g. "claude-code", "codex").</summary>
    public string? ProviderId { get; set; }

    /// <summary>The primary model id chosen for this drive (overrides per-agent models). Null = use team defaults.</summary>
    public string? ModelId { get; set; }

    /// <summary>The reasoning effort level for the primary model.</summary>
    public AgentEffort Effort { get; set; } = AgentEffort.High;

    /// <summary>
    /// When true, every squad agent runs on the Quarterback's model + effort (the drive primary).
    /// When false, each squad member keeps its own model + effort from the team.
    /// </summary>
    public bool OverrideTeamConfig { get; set; } = true;
}

public class HuddleMessage
{
    public string Role { get; set; } = string.Empty; // "user" | "quarterback" | "coordinator" | "rollcall"
    public string? Name { get; set; }                 // display name of the speaker (e.g. "Head Coach", "The Scout")
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AgentMessage
{
    public string DriveId { get; set; } = string.Empty;
    public string FromAgent { get; set; } = string.Empty;
    public string ToAgent { get; set; } = "all";
    public PlayType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum PlayType
{
    Assignment,
    PlayComplete,
    HandoffRequest,
    TurnoverAlert,
    TouchdownSignal,
    StatusUpdate,
    ReviewRequest,
    ReviewApproved,
    ReviewRejected
}
