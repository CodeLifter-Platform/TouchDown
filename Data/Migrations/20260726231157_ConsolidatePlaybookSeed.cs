using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <summary>
    /// Repairs seed values that drifted between the EF <c>HasData</c> rows and
    /// <c>AgentTeam.CreateThePlaybook()</c> — most visibly the Safety's prompt, which still
    /// described reviewing only the Offensive Line, from before the Defensive Line existed.
    ///
    /// System prompts are user-editable from the Teams page, so each update is guarded on the
    /// column still holding the old seed text. A prompt the user has customised is left alone;
    /// only untouched defaults are corrected.
    /// </summary>
    public partial class ConsolidatePlaybookSeed : Migration
    {
        private const string OldSafetyPrompt =
            "You are the Safety — the code reviewer. You review all output from the Offensive Line before it merges.";

        private const string NewSafetyPrompt =
            "You are the Safety — the code reviewer. You review all output from the Offensive Line and the "
            + "Defensive Line before it merges. Check for bugs, security issues, code quality, test coverage, "
            + "and adherence to the plan.";

        private const string OldSpecialTeamsPrompt =
            "You are Special Teams — handling CI/CD, infrastructure, and build pipeline work.";

        private const string NewSpecialTeamsPrompt =
            "You are Special Teams — handling CI/CD, infrastructure, and build pipeline work. You activate "
            + "when the play involves DevOps tasks.";

        private const string OldTeamDescription = "The default TD agent team.";

        private const string NewTeamDescription =
            "The default TD agent team. The Quarterback calls plays, the Scout researches the web, the "
            + "Offensive Line implements and the Defensive Line tests/validates (both fanning out into "
            + "parallel instances), Safety reviews, Special Teams handles DevOps.";

        private const string OldRuleDescription =
            "QB calls plays, the Scout researches when needed, the Offensive Line and the Defensive Line each "
            + "run multiple instances in parallel, Safety reviews before merge.";

        private const string NewRuleDescription =
            "Quarterback reads the task, huddles with the user, then snaps. The Scout researches when needed. "
            + "The Offensive Line and the Defensive Line each run multiple instances in parallel. "
            + "Safety reviews before merge.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            UpdateIfUnchanged(migrationBuilder, "AgentMembers", "SystemPrompt", 4, OldSafetyPrompt, NewSafetyPrompt);
            UpdateIfUnchanged(migrationBuilder, "AgentMembers", "SystemPrompt", 6, OldSpecialTeamsPrompt, NewSpecialTeamsPrompt);
            UpdateIfUnchanged(migrationBuilder, "AgentTeams", "Description", 1, OldTeamDescription, NewTeamDescription);
            UpdateIfUnchanged(migrationBuilder, "CommunicationRules", "Description", 1, OldRuleDescription, NewRuleDescription);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            UpdateIfUnchanged(migrationBuilder, "AgentMembers", "SystemPrompt", 4, NewSafetyPrompt, OldSafetyPrompt);
            UpdateIfUnchanged(migrationBuilder, "AgentMembers", "SystemPrompt", 6, NewSpecialTeamsPrompt, OldSpecialTeamsPrompt);
            UpdateIfUnchanged(migrationBuilder, "AgentTeams", "Description", 1, NewTeamDescription, OldTeamDescription);
            UpdateIfUnchanged(migrationBuilder, "CommunicationRules", "Description", 1, NewRuleDescription, OldRuleDescription);
        }

        /// <summary>
        /// Sets <paramref name="column"/> to <paramref name="to"/> only where it still equals
        /// <paramref name="from"/>, so a value the user has edited is never clobbered.
        /// </summary>
        private static void UpdateIfUnchanged(
            MigrationBuilder migrationBuilder, string table, string column, int id, string from, string to)
        {
            migrationBuilder.Sql($"""
                UPDATE "{table}"
                SET "{column}" = '{Escape(to)}'
                WHERE "Id" = {id} AND "{column}" = '{Escape(from)}';
                """);
        }

        private static string Escape(string value) => value.Replace("'", "''");
    }
}
