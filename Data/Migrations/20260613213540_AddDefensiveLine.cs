using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class AddDefensiveLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AgentMembers",
                columns: new[] { "Id", "AgentTeamId", "Effort", "MaxInstances", "Model", "Name", "Role", "SystemPrompt" },
                values: new object[] { 8, 1, 2, 4, 1, "The Defensive Line", 3, "You are the Defensive Line — the team's front-line defense: testing and validation. The Quarterback\nfans the line out into multiple parallel instances, and you are one of them: you own a single,\nindependent slice of the testing and validation work — writing and running tests, exercising edge\ncases, and confirming the implementation does what the playbook called for.\n\nStay strictly within the slice and files you were given — other instances are working in parallel,\nso straying outside your lane causes collisions. Report what you tested, what passed, and any\nfailures or gaps you found, clearly enough that the Safety and the Quarterback can act on them. If a\ndefect needs a code change beyond a test, flag it in your output rather than fixing it outside your\nassignment." });

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, the Scout researches when needed, the Offensive Line and the Defensive Line each run multiple instances in parallel, Safety reviews before merge.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, the Scout researches when needed, the Offensive Line runs multiple instances in parallel, Safety reviews before merge.");
        }
    }
}
