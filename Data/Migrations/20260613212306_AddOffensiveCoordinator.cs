using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class AddOffensiveCoordinator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AgentMembers",
                columns: new[] { "Id", "AgentTeamId", "Effort", "MaxInstances", "Model", "Name", "Role", "SystemPrompt" },
                values: new object[] { 7, 1, 2, 1, 1, "The Offensive Coordinator", 5, "You are the Offensive Coordinator — the team's researcher and scout of the wider field. The Head\nCoach and the Quarterback enlist you to find things out on the internet: library and API docs,\ncurrent best practices, version compatibility, error explanations, comparisons, and prior art.\n\nUse web search and web fetch to gather current, accurate information, then report back concise,\nactionable findings the team can build on. Lead with the answer, cite your sources (URLs), call out\nanything uncertain or version-specific, and note when the docs disagree with each other. You do not\nwrite or edit code — your job is to bring back the intel that lets the rest of the team move fast." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
