using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TD.Models;

public enum DriveStatus
{
    Pending,
    Huddle,
    InProgress,
    Touchdown,
    Turnover,
    Cancelled
}

public class Drive
{
    public int Id { get; set; }

    [Required]
    public string DriveId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Optional human-friendly name for the drive. Falls back to the task / id for display.</summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    public DriveStatus Status { get; set; } = DriveStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [Required, MaxLength(2000)]
    public string TaskDescription { get; set; } = string.Empty;

    public int MaxParallelism { get; set; } = 2;

    public SourceType SourceType { get; set; }

    [MaxLength(500)]
    public string? RepoPath { get; set; }

    [MaxLength(200)]
    public string? Branch { get; set; }

    public WorkspaceMode WorkspaceMode { get; set; }

    [MaxLength(500)]
    public string? WorkspacePath { get; set; }

    [MaxLength(200)]
    public string? PrBranchName { get; set; }

    public int AgentTeamId { get; set; }
    public AgentTeam? AgentTeam { get; set; }

    public List<Play> Plays { get; set; } = [];
    public List<DriveLog> Logs { get; set; } = [];
    public List<DriveTurn> Turns { get; set; } = [];

    [MaxLength(10000)]
    public string? HuddlePlan { get; set; }

    /// <summary>The agent provider used for this drive (e.g. "claude-code", "codex").</summary>
    [MaxLength(50)]
    public string? ProviderId { get; set; }

    /// <summary>The primary model id chosen for this drive (overrides per-agent models). Null = use team defaults.</summary>
    [MaxLength(100)]
    public string? ModelId { get; set; }

    /// <summary>The reasoning effort level for the primary model.</summary>
    public AgentEffort Effort { get; set; } = AgentEffort.High;

    /// <summary>
    /// When true, every squad agent runs on this drive's primary model + effort (the Quarterback's config).
    /// When false, each squad member keeps its own model + effort from the team.
    /// </summary>
    public bool OverrideTeamConfig { get; set; } = true;

    /// <summary>What to call this drive in the UI: the name if set, else a trimmed task, else the id.</summary>
    [NotMapped]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name
        : !string.IsNullOrWhiteSpace(TaskDescription)
            ? (TaskDescription.Length > 60 ? TaskDescription[..57] + "…" : TaskDescription)
            : DriveId;
}
