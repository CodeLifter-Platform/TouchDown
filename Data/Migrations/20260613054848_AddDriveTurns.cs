using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class AddDriveTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriveTurns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DriveId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayId = table.Column<int>(type: "INTEGER", nullable: true),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 200000, nullable: false),
                    ToolsUsed = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CostUsd = table.Column<double>(type: "REAL", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriveTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriveTurns_Drives_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriveTurns_DriveId_Timestamp",
                table: "DriveTurns",
                columns: new[] { "DriveId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriveTurns");
        }
    }
}
