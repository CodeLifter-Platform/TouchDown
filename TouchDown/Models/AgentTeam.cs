using System.ComponentModel.DataAnnotations;

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

    public static AgentTeam CreateThePlaybook() => new()
    {
        Name = "The Playbook",
        Description = "The default TD agent team. The Quarterback calls plays, Guards implement, Safety reviews, Scout tests, Special Teams handles DevOps.",
        IsDefault = true,
        Members =
        [
            new() { Name = "The Quarterback", Role = AgentRole.Leader, Model = ClaudeModel.Opus, SystemPrompt = "You are the Quarterback — the team leader. You read the task, create a structured plan, delegate assignments to your team, and coordinate the drive to completion. You own the plan and make the final call." },
            new() { Name = "Left Guard", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Left Guard — a core implementer. You receive your assignment from the Quarterback and execute it with precision. Focus on clean, working code." },
            new() { Name = "Right Guard", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Right Guard — a parallel implementer. You work alongside the Left Guard on your assigned portion. Focus on clean, working code that integrates with the team's output." },
            new() { Name = "The Safety", Role = AgentRole.Validator, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Safety — the code reviewer. You review all output from the Guards before it merges. Check for bugs, security issues, code quality, and adherence to the plan." },
            new() { Name = "The Scout", Role = AgentRole.Tester, Model = ClaudeModel.Haiku, SystemPrompt = "You are the Scout — fast and lightweight. You write and run tests concurrently with implementation. Focus on coverage and catching regressions early." },
            new() { Name = "Special Teams", Role = AgentRole.DevOps, Model = ClaudeModel.Haiku, SystemPrompt = "You are Special Teams — handling CI/CD, infrastructure, and build pipeline work. You activate when the play involves DevOps tasks." },
        ],
        CommunicationRules =
        [
            new() { Style = CommStyle.LeaderGated, Description = "Quarterback reads the task, huddles with the user, then snaps. Guards run in parallel. Safety reviews before merge. Scout runs concurrently." }
        ]
    };
}
