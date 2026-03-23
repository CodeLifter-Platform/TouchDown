using System.ComponentModel.DataAnnotations;

namespace TouchDown.Models;

public enum PlayStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

public class Play
{
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public PlayStatus Status { get; set; } = PlayStatus.Pending;

    public int? AssignedMemberId { get; set; }
    public AgentMember? AssignedMember { get; set; }

    public int DriveId { get; set; }
    public Drive? Drive { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(50000)]
    public string? Output { get; set; }

    public int OrderIndex { get; set; }
}
