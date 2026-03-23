using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AgentTeamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMembers_AgentTeams_AgentTeamId",
                        column: x => x.AgentTeamId,
                        principalTable: "AgentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Style = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    AgentTeamId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationRules_AgentTeams_AgentTeamId",
                        column: x => x.AgentTeamId,
                        principalTable: "AgentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DriveId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TaskDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    MaxParallelism = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    RepoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WorkspaceMode = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkspacePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrBranchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AgentTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    HuddlePlan = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drives_AgentTeams_AgentTeamId",
                        column: x => x.AgentTeamId,
                        principalTable: "AgentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DriveLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    DriveId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriveLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriveLogs_Drives_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Plays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedMemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    DriveId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Output = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: true),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plays_AgentMembers_AssignedMemberId",
                        column: x => x.AssignedMemberId,
                        principalTable: "AgentMembers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Plays_Drives_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AgentTeams",
                columns: new[] { "Id", "Description", "IsDefault", "Name" },
                values: new object[] { 1, "The default TD agent team.", true, "The Playbook" });

            migrationBuilder.InsertData(
                table: "AgentMembers",
                columns: new[] { "Id", "AgentTeamId", "Model", "Name", "Role", "SystemPrompt" },
                values: new object[,]
                {
                    { 1, 1, 0, "The Quarterback", 0, "You are the Quarterback — the team leader. You read the task, create a structured plan, delegate assignments to your team, and coordinate the drive to completion." },
                    { 2, 1, 1, "Left Guard", 1, "You are the Left Guard — a core implementer. You receive your assignment from the Quarterback and execute it with precision." },
                    { 3, 1, 1, "Right Guard", 1, "You are the Right Guard — a parallel implementer. You work alongside the Left Guard on your assigned portion." },
                    { 4, 1, 1, "The Safety", 2, "You are the Safety — the code reviewer. You review all output from the Guards before it merges." },
                    { 5, 1, 2, "The Scout", 3, "You are the Scout — fast and lightweight. You write and run tests concurrently with implementation." },
                    { 6, 1, 2, "Special Teams", 4, "You are Special Teams — handling CI/CD, infrastructure, and build pipeline work." }
                });

            migrationBuilder.InsertData(
                table: "CommunicationRules",
                columns: new[] { "Id", "AgentTeamId", "Description", "Style" },
                values: new object[] { 1, 1, "QB calls plays, Guards run parallel, Safety reviews before merge, Scout runs concurrently.", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMembers_AgentTeamId",
                table: "AgentMembers",
                column: "AgentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRules_AgentTeamId",
                table: "CommunicationRules",
                column: "AgentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DriveLogs_DriveId",
                table: "DriveLogs",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_Drives_AgentTeamId",
                table: "Drives",
                column: "AgentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Drives_DriveId",
                table: "Drives",
                column: "DriveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plays_AssignedMemberId",
                table: "Plays",
                column: "AssignedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Plays_DriveId",
                table: "Plays",
                column: "DriveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationRules");

            migrationBuilder.DropTable(
                name: "DriveLogs");

            migrationBuilder.DropTable(
                name: "Plays");

            migrationBuilder.DropTable(
                name: "AgentMembers");

            migrationBuilder.DropTable(
                name: "Drives");

            migrationBuilder.DropTable(
                name: "AgentTeams");
        }
    }
}
