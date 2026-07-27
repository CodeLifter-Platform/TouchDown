using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TD.Models;

public class AgentTeam
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public List<AgentMember> Members { get; set; } = [];
    public List<CommunicationRule> CommunicationRules { get; set; } = [];

    public AgentMember? GetLeader() => Members.FirstOrDefault(m => m.IsLeader);

    /// <summary>
    /// A context block, injected into every agent's prompt, that names the real roster. Without it an
    /// agent only knows about the Claude Code subagents/plugins in its environment and will report those
    /// when asked who "the team" is — this anchors it to its actual TouchDown teammates.
    /// </summary>
    public string BuildRosterPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Your Team — \"{Name}\"");
        sb.AppendLine("You are a player on this TouchDown agent team. Stay in character as your role. Your ONLY");
        sb.AppendLine("teammates are the players listed below — you are not a generic assistant and not an external");
        sb.AppendLine("Claude Code subagent or plugin. If the Head Coach asks who is on the team, use exactly this");
        sb.AppendLine("roster and no one else:");
        sb.AppendLine("You speak ONLY for yourself. Never fabricate, impersonate, quote, or invent a teammate's words,");
        sb.AppendLine("name, or status — only each teammate can answer for itself. If the Head Coach wants the whole");
        sb.AppendLine("team to report or roll-call, do NOT make up their answers; that runs as a real roll call where");
        sb.AppendLine("each teammate checks in for itself.");
        foreach (var m in Members.OrderBy(m => m.Role))
        {
            var fanout = m.MaxInstances > 1 ? $"; fans out into up to {m.MaxInstances} parallel instances" : "";
            sb.AppendLine($"- {m.Name} — {RoleBlurb(m.Role)}{fanout}");
        }
        return sb.ToString();
    }

    private static string RoleBlurb(AgentRole role) => role switch
    {
        AgentRole.Leader => "the Quarterback; calls the plays and coordinates the drive",
        AgentRole.Researcher => "researches the web for docs, APIs, and best practices",
        AgentRole.Worker => "implements the feature work",
        AgentRole.Tester => "writes and runs tests and validates the work",
        AgentRole.Validator => "reviews all code before it merges",
        AgentRole.DevOps => "handles CI/CD, infrastructure, and the build pipeline",
        _ => role.ToString()
    };

    /// <summary>
    /// Builds the built-in team from <see cref="PlaybookSeed"/> — the same rows EF seeds into
    /// the database, so this factory and the persisted team can never disagree.
    /// </summary>
    public static AgentTeam CreateThePlaybook() => new()
    {
        Id = PlaybookSeed.TeamId,
        Name = PlaybookSeed.TeamName,
        Description = PlaybookSeed.TeamDescription,
        IsDefault = true,
        Members = PlaybookSeed.Members.Select(m => new AgentMember
        {
            Id = m.Id,
            Name = m.Name,
            Role = m.Role,
            Model = m.Model,
            Effort = m.Effort,
            MaxInstances = m.MaxInstances,
            SystemPrompt = m.SystemPrompt,
            AgentTeamId = PlaybookSeed.TeamId,
        }).ToList(),
        CommunicationRules =
        [
            new()
            {
                Id = PlaybookSeed.CommunicationRuleId,
                Style = PlaybookSeed.CommunicationStyle,
                Description = PlaybookSeed.CommunicationRuleDescription,
                AgentTeamId = PlaybookSeed.TeamId,
            }
        ]
    };
}
