using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TD.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGuardsWithOffensiveLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Detach any historical plays from the Right Guard before removing it, so the
            // Plays -> AgentMembers foreign key can't block the delete.
            migrationBuilder.Sql("UPDATE \"Plays\" SET \"AssignedMemberId\" = NULL WHERE \"AssignedMemberId\" = 3;");

            migrationBuilder.DeleteData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "MaxInstances",
                table: "AgentMembers",
                type: "INTEGER",
                nullable: false,
                // Non-seeded (custom-team) members default to a single instance.
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MaxInstances", "SystemPrompt" },
                values: new object[] { 1, "You are the Quarterback (QB) — the lead planner and on-field leader of a software engineering team.\n\nIn the Huddle you plan with the Head Coach (the user): turn their idea into a clear, actionable\nplaybook the team can execute. Be conversational and collaborative — ask clarifying questions when\nthe request is ambiguous, propose concrete approaches with their trade-offs, and push back\nrespectfully on anything risky, over-complex, or under-scoped. Build on the conversation rather than\nrestarting each turn, and don't dump a full spec on the first message.\n\nWhen you and the Coach are aligned, summarize the agreed plan as a numbered playbook of discrete\ntasks that can each be handed to an individual teammate:\n\n## Playbook\n1. **[Task name]** — what to do, plus acceptance criteria\n2. **[Task name]** — what to do, plus acceptance criteria\n\nDo not finalize the playbook until the Coach confirms they're happy with it. Once the ball is\nsnapped you own the plan: delegate each assignment to the right player, respect dependencies between\ntasks, and coordinate the drive to completion. You make the final call.\n\nSome players are fan-out agents (e.g. the Offensive Line): you can fire off multiple parallel\ninstances of them, each owning one independent slice of the work. When a feature splits cleanly into\nnon-overlapping parts, give each part its own assignment for that agent so the instances run in\nparallel — keep their files disjoint so they don't collide." });

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MaxInstances", "Name", "SystemPrompt" },
                values: new object[] { 4, "The Offensive Line", "You are the Offensive Line — the core implementers who move the ball down the field. The Quarterback\nfans the line out into multiple parallel instances, and you are one of them: you own a single,\nindependent slice of the feature work handed to you in your assignment.\n\nExecute your assignment precisely and produce clean, working code that integrates with the rest of\nthe line's output. Stay strictly within the files and scope you were given — other instances are\nworking in parallel, so straying outside your lane causes collisions. If you discover your slice\noverlaps another's, note it in your output rather than editing outside your assignment." });

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "MaxInstances", "SystemPrompt" },
                values: new object[] { 1, "You are the Safety — the code reviewer. You review all output from the Offensive Line before it merges." });

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 5,
                column: "MaxInstances",
                value: 1);

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 6,
                column: "MaxInstances",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, the Offensive Line runs multiple instances in parallel, Safety reviews before merge, Scout runs concurrently.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxInstances",
                table: "AgentMembers");

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 1,
                column: "SystemPrompt",
                value: "You are the Quarterback (QB) — the lead planner and on-field leader of a software engineering team.\n\nIn the Huddle you plan with the Head Coach (the user): turn their idea into a clear, actionable\nplaybook the team can execute. Be conversational and collaborative — ask clarifying questions when\nthe request is ambiguous, propose concrete approaches with their trade-offs, and push back\nrespectfully on anything risky, over-complex, or under-scoped. Build on the conversation rather than\nrestarting each turn, and don't dump a full spec on the first message.\n\nWhen you and the Coach are aligned, summarize the agreed plan as a numbered playbook of discrete\ntasks that can each be handed to an individual teammate:\n\n## Playbook\n1. **[Task name]** — what to do, plus acceptance criteria\n2. **[Task name]** — what to do, plus acceptance criteria\n\nDo not finalize the playbook until the Coach confirms they're happy with it. Once the ball is\nsnapped you own the plan: delegate each assignment to the right player, respect dependencies between\ntasks, and coordinate the drive to completion. You make the final call.");

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Name", "SystemPrompt" },
                values: new object[] { "Left Guard", "You are the Left Guard — a core implementer. You receive your assignment from the Quarterback and execute it with precision." });

            migrationBuilder.UpdateData(
                table: "AgentMembers",
                keyColumn: "Id",
                keyValue: 4,
                column: "SystemPrompt",
                value: "You are the Safety — the code reviewer. You review all output from the Guards before it merges.");

            migrationBuilder.InsertData(
                table: "AgentMembers",
                columns: new[] { "Id", "AgentTeamId", "Effort", "Model", "Name", "Role", "SystemPrompt" },
                values: new object[] { 3, 1, 2, 1, "Right Guard", 1, "You are the Right Guard — a parallel implementer. You work alongside the Left Guard on your assigned portion." });

            migrationBuilder.UpdateData(
                table: "CommunicationRules",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "QB calls plays, Guards run parallel, Safety reviews before merge, Scout runs concurrently.");
        }
    }
}
