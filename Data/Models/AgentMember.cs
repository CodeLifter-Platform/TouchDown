using System.ComponentModel.DataAnnotations;

namespace TD.Models;

public class AgentMember
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public AgentRole Role { get; set; }

    public ClaudeModel Model { get; set; }

    public bool IsLeader => Role == AgentRole.Leader;

    [MaxLength(500)]
    public string? SystemPrompt { get; set; }

    public int AgentTeamId { get; set; }
    public AgentTeam? AgentTeam { get; set; }
}
