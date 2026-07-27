namespace TD.Models;

/// <summary>
/// The one definition of the built-in "The Playbook" team.
///
/// This used to exist twice — as EF <c>HasData</c> rows in TDDbContext and again as
/// <see cref="AgentTeam.CreateThePlaybook"/> — and the two had drifted: the seeded Safety
/// prompt still described reviewing only the Offensive Line, from before the Defensive Line
/// existed. Both now project from the rows below, so they cannot disagree again.
///
/// The ids are fixed and non-contiguous (4, 5, 6, 8) because installed databases already
/// carry them from earlier roster changes; renumbering would rewrite every existing row.
/// </summary>
public static class PlaybookSeed
{
    public const int TeamId = 1;
    public const int CommunicationRuleId = 1;

    public const string TeamName = "The Playbook";

    public const string TeamDescription =
        "The default TD agent team. The Quarterback calls plays, the Scout researches the web, the "
        + "Offensive Line implements and the Defensive Line tests/validates (both fanning out into "
        + "parallel instances), Safety reviews, Special Teams handles DevOps.";

    public const CommStyle CommunicationStyle = CommStyle.LeaderGated;

    public const string CommunicationRuleDescription =
        "Quarterback reads the task, huddles with the user, then snaps. The Scout researches when needed. "
        + "The Offensive Line and the Defensive Line each run multiple instances in parallel. "
        + "Safety reviews before merge.";

    /// <summary>One seeded roster member. Mirrors the persisted columns exactly.</summary>
    public readonly record struct MemberSeed(
        int Id,
        string Name,
        AgentRole Role,
        ClaudeModel Model,
        AgentEffort Effort,
        int MaxInstances,
        string SystemPrompt);

    public static IReadOnlyList<MemberSeed> Members { get; } =
    [
        new(1, "The Quarterback",     AgentRole.Leader,     ClaudeModel.Opus,   AgentEffort.High,   1, AgentDefaults.QuarterbackSystemPrompt),
        new(2, "The Offensive Line",  AgentRole.Worker,     ClaudeModel.Sonnet, AgentEffort.High,   4, AgentDefaults.OffensiveLineSystemPrompt),
        new(4, "The Safety",          AgentRole.Validator,  ClaudeModel.Sonnet, AgentEffort.High,   1, AgentDefaults.SafetySystemPrompt),
        new(5, "The Scout",           AgentRole.Researcher, ClaudeModel.Sonnet, AgentEffort.High,   1, AgentDefaults.ScoutSystemPrompt),
        new(6, "Special Teams",       AgentRole.DevOps,     ClaudeModel.Haiku,  AgentEffort.Medium, 1, AgentDefaults.SpecialTeamsSystemPrompt),
        new(8, "The Defensive Line",  AgentRole.Tester,     ClaudeModel.Sonnet, AgentEffort.High,   4, AgentDefaults.DefensiveLineSystemPrompt),
    ];
}
