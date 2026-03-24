using System.Text.Json;
using System.Text.RegularExpressions;
using TD.Models;

namespace TD.Services;

public interface IPlanParserService
{
    Task<QuarterbackPlan> ParsePlanFromHuddleAsync(
        string huddleOutput,
        AgentTeam team,
        string taskDescription,
        IAgentProvider provider,
        string? workingDirectory,
        CancellationToken ct = default);

    List<Play> ConvertPlanToPlays(QuarterbackPlan plan, AgentTeam team);
}

public partial class PlanParserService : IPlanParserService
{
    private readonly ILogger<PlanParserService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public PlanParserService(ILogger<PlanParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Uses the Quarterback to produce a structured JSON plan from the huddle conversation.
    /// If the huddle output already contains valid JSON, uses it directly.
    /// Otherwise, calls the QB again with a structured output prompt.
    /// </summary>
    public async Task<QuarterbackPlan> ParsePlanFromHuddleAsync(
        string huddleOutput,
        AgentTeam team,
        string taskDescription,
        IAgentProvider provider,
        string? workingDirectory,
        CancellationToken ct = default)
    {
        // Try extracting JSON from the huddle output first
        var plan = TryExtractPlanJson(huddleOutput);
        if (plan != null && plan.Assignments.Count > 0)
        {
            _logger.LogInformation("Extracted structured plan from huddle output ({Count} assignments)", plan.Assignments.Count);
            return plan;
        }

        // Otherwise, ask the QB to produce a structured plan
        var qb = team.GetLeader();
        var memberList = string.Join("\n", team.Members.Select(m =>
            $"- {m.Name} (Role: {m.Role}, Model: {m.Model.ToDisplayName()})"));

        var structuredPrompt = $$"""
            You previously discussed this task with the user:

            TASK: {{taskDescription}}

            YOUR PREVIOUS ANALYSIS:
            {{huddleOutput}}

            TEAM MEMBERS:
            {{memberList}}

            Now produce a STRUCTURED PLAN as a JSON object. The JSON must follow this exact schema:
            {
              "summary": "Brief summary of the plan",
              "assignments": [
                {
                  "agent_role": "Worker|Tester|Validator|DevOps",
                  "agent_name": "The specific agent name from your team",
                  "task": "Detailed description of what this agent should do",
                  "depends_on": [],
                  "files": ["list of files this agent will likely touch"],
                  "priority": 1
                }
              ]
            }

            Rules:
            - Split work between Workers (Left Guard, Right Guard) so they can work in parallel
            - Workers should NOT overlap on the same files
            - The Scout (Tester) runs concurrently with Workers
            - The Safety (Validator) runs AFTER Workers complete
            - Special Teams (DevOps) only gets an assignment if the task involves CI/CD/infra
            - Each assignment's "depends_on" is a list of assignment indices (0-based) that must complete first
            - Respond ONLY with the JSON, no other text
            """;

        var response = await provider.RunAsync(new AgentContext
        {
            ModelId = qb?.Model.ToModelId() ?? ClaudeModel.Opus.ToModelId(),
            SystemPrompt = qb?.SystemPrompt ?? "You are the Quarterback. Produce structured plans.",
            Prompt = structuredPrompt,
            WorkingDirectory = workingDirectory,
        }, ct);

        var result = response.FullText;

        plan = TryExtractPlanJson(result);
        if (plan != null && plan.Assignments.Count > 0)
        {
            _logger.LogInformation("QB produced structured plan ({Count} assignments)", plan.Assignments.Count);
            return plan;
        }

        // Fallback: create a basic plan from the task description
        _logger.LogWarning("Failed to extract structured plan, falling back to basic splitting");
        return CreateFallbackPlan(taskDescription, team);
    }

    public List<Play> ConvertPlanToPlays(QuarterbackPlan plan, AgentTeam team)
    {
        var plays = new List<Play>();

        for (int i = 0; i < plan.Assignments.Count; i++)
        {
            var assignment = plan.Assignments[i];

            // Match assignment to team member by name or role
            var member = !string.IsNullOrEmpty(assignment.AgentName)
                ? team.Members.FirstOrDefault(m =>
                    m.Name.Equals(assignment.AgentName, StringComparison.OrdinalIgnoreCase))
                : null;

            member ??= team.Members.FirstOrDefault(m =>
                m.Role.ToString().Equals(assignment.AgentRole, StringComparison.OrdinalIgnoreCase));

            // Skip if no matching member found
            if (member == null)
            {
                _logger.LogWarning("No team member found for assignment: {Role}/{Name}", assignment.AgentRole, assignment.AgentName);
                continue;
            }

            plays.Add(new Play
            {
                Description = assignment.Task,
                AssignedMember = member,
                AssignedMemberId = member.Id,
                OrderIndex = i,
            });
        }

        return plays;
    }

    private QuarterbackPlan? TryExtractPlanJson(string text)
    {
        // Try the whole text as JSON
        try
        {
            var plan = JsonSerializer.Deserialize<QuarterbackPlan>(text, JsonOptions);
            if (plan?.Assignments.Count > 0) return plan;
        }
        catch { /* not pure JSON */ }

        // Try to find JSON block in markdown code fence
        var match = JsonCodeBlockRegex().Match(text);
        if (match.Success)
        {
            try
            {
                var plan = JsonSerializer.Deserialize<QuarterbackPlan>(match.Groups[1].Value, JsonOptions);
                if (plan?.Assignments.Count > 0) return plan;
            }
            catch { /* bad JSON in code block */ }
        }

        // Try to find any JSON object that looks like a plan
        var braceStart = text.IndexOf('{');
        var braceEnd = text.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            try
            {
                var jsonCandidate = text[braceStart..(braceEnd + 1)];
                var plan = JsonSerializer.Deserialize<QuarterbackPlan>(jsonCandidate, JsonOptions);
                if (plan?.Assignments.Count > 0) return plan;
            }
            catch { /* not valid JSON */ }
        }

        return null;
    }

    private static QuarterbackPlan CreateFallbackPlan(string taskDescription, AgentTeam team)
    {
        var assignments = new List<PlayAssignment>();
        var workers = team.Members.Where(m => m.Role == AgentRole.Worker).ToList();
        var tester = team.Members.FirstOrDefault(m => m.Role == AgentRole.Tester);
        var safety = team.Members.FirstOrDefault(m => m.Role == AgentRole.Validator);
        var devops = team.Members.FirstOrDefault(m => m.Role == AgentRole.DevOps);

        // Split work across workers
        for (int i = 0; i < workers.Count; i++)
        {
            assignments.Add(new PlayAssignment
            {
                AgentRole = "Worker",
                AgentName = workers[i].Name,
                Task = workers.Count > 1
                    ? $"Implement part {i + 1} of {workers.Count} for: {taskDescription}"
                    : taskDescription,
                Priority = 1,
                DependsOn = []
            });
        }

        // Tester runs concurrently (no dependencies)
        if (tester != null)
        {
            assignments.Add(new PlayAssignment
            {
                AgentRole = "Tester",
                AgentName = tester.Name,
                Task = $"Write comprehensive tests for: {taskDescription}",
                Priority = 1,
                DependsOn = []
            });
        }

        // Safety reviews after workers
        if (safety != null)
        {
            var workerIndices = Enumerable.Range(0, workers.Count).ToList();
            assignments.Add(new PlayAssignment
            {
                AgentRole = "Validator",
                AgentName = safety.Name,
                Task = $"Review all code changes for quality, security, and correctness: {taskDescription}",
                Priority = 2,
                DependsOn = workerIndices
            });
        }

        return new QuarterbackPlan
        {
            Summary = $"Fallback plan: {workers.Count} workers + tester (parallel), then safety review",
            Assignments = assignments
        };
    }

    [GeneratedRegex(@"```(?:json)?\s*\n([\s\S]*?)\n```", RegexOptions.Multiline)]
    private static partial Regex JsonCodeBlockRegex();
}
