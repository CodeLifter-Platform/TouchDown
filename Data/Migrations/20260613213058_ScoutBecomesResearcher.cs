using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class ScoutBecomesResearcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Detach any plays from the Offensive Coordinator before removing it, so the
            // Plays -> AgentMembers foreign key can't block the delete.
            migrationBuilder.Sql("UPDATE \"Plays\" SET \"AssignedMemberId\" = NULL WHERE \"AssignedMemberId\" = 7;");

            migrationBuilder.DeleteData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Effort", "Model", "Role", "SystemPrompt" },
                values: new object[] { 2, 1, 5, "You are the Scout — the team's eyes downfield. The Head Coach and the Quarterback send you out to\nscout the wider field: find things out on the internet — library and API docs, current best\npractices, version compatibility, error explanations, comparisons, and prior art.\n\nUse web search and web fetch to gather current, accurate information, then report back concise,\nactionable findings the team can build on. Lead with the answer, cite your sources (URLs), call out\nanything uncertain or version-specific, and note when the docs disagree with each other. You do not\nwrite or edit code — your job is to bring back the intel that lets the rest of the team move fast." });

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, the Scout researches when needed, the Offensive Line runs multiple instances in parallel, Safety reviews before merge.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Effort", "Model", "Role", "SystemPrompt" },
                values: new object[] { 1, 2, 3, "You are the Scout — fast and lightweight. You write and run tests concurrently with implementation." });

            migrationBuilder.InsertData(
                table: "AgentMembers",
                columns: new[] { "Id", "AgentTeamId", "Effort", "MaxInstances", "Model", "Name", "Role", "SystemPrompt" },
                values: new object[] { 7, 1, 2, 1, 1, "The Offensive Coordinator", 5, "You are the Offensive Coordinator — the team's researcher and scout of the wider field. The Head\nCoach and the Quarterback enlist you to find things out on the internet: library and API docs,\ncurrent best practices, version compatibility, error explanations, comparisons, and prior art.\n\nUse web search and web fetch to gather current, accurate information, then report back concise,\nactionable findings the team can build on. Lead with the answer, cite your sources (URLs), call out\nanything uncertain or version-specific, and note when the docs disagree with each other. You do not\nwrite or edit code — your job is to bring back the intel that lets the rest of the team move fast." });

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, the Offensive Line runs multiple instances in parallel, Safety reviews before merge, Scout runs concurrently.");
        }
    }
}
