using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberEffortAndDriveOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OverrideTeamConfig",
                table: "Drives",
                type: "INTEGER",
                nullable: false,
                // Existing drives ran under the old override-all behavior.
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Effort",
                table: "AgentMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 1,
                column: "Effort",
                value: 2);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 2,
                column: "Effort",
                value: 2);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 3,
                column: "Effort",
                value: 2);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 4,
                column: "Effort",
                value: 2);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 5,
                column: "Effort",
                value: 1);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 6,
                column: "Effort",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideTeamConfig",
                table: "Drives");

            migrationBuilder.DropColumn(
                name: "Effort",
                table: "AgentMembers");
        }
    }
}
