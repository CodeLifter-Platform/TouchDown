using System.ComponentModel.DataAnnotations;

namespace TD.Models;

public class AgentMember
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public AgentRole Role { get; set; }

    public ClaudeModel Model { get; set; }

    /// <summary>This agent's own reasoning effort, used when the drive does not override team settings.</summary>
    public AgentEffort Effort { get; set; } = AgentEffort.High;

    /// <summary>
    /// Maximum parallel instances the Quarterback may fan this agent out into for parallelizable work.
    /// 1 = a single instance; greater than 1 marks a fan-out agent (e.g. the Offensive Line).
    /// </summary>
    public int MaxInstances { get; set; } = 1;

    public bool IsLeader => Role == AgentRole.Leader;

    [MaxLength(4000)]
    public string? SystemPrompt { get; set; }

    public int AgentTeamId { get; set; }
    public AgentTeam? AgentTeam { get; set; }
}
