using System.ComponentModel.DataAnnotations;

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

    [MaxLength(10000)]
    public string? HuddlePlan { get; set; }

    /// <summary>The agent provider used for this drive (e.g. "claude-code", "codex").</summary>
    [MaxLength(50)]
    public string? ProviderId { get; set; }
}
