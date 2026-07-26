using Microsoft.Extensions.Logging.Abstractions;
using TD.Models;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// Plan extraction runs before any model call. When the huddle already contains a usable
/// plan, re-prompting the Quarterback costs a second Opus call for nothing — so these
/// tests assert extraction succeeds without touching the provider.
/// </summary>
public class PlanExtractionTests
{
    private const string ValidPlanJson = """
        {
          "summary": "Ship the login page",
          "assignments": [
            {"agent_role":"Worker","agent_name":"The Offensive Line","task":"build the form","depends_on":[],"priority":1},
            {"agent_role":"Validator","agent_name":"The Safety","task":"review it","depends_on":[0],"priority":2}
          ]
        }
        """;

    private static PlanParserService CreateParser() => new(NullLogger<PlanParserService>.Instance);

    /// <summary>The provider throws if called, so a passing test proves nothing was re-prompted.</summary>
    private static Task<QuarterbackPlan> Extract(string huddleOutput) =>
        CreateParser().ParsePlanFromHuddleAsync(
            huddleOutput,
            AgentTeam.CreateThePlaybook(),
            "add a login page",
            new FakeAgentProvider(),
            workingDirectory: null);

    [Fact]
    public async Task RawJson_IsUsedDirectly()
    {
        var plan = await Extract(ValidPlanJson);

        Assert.Equal("Ship the login page", plan.Summary);
        Assert.Equal(2, plan.Assignments.Count);
        Assert.Equal("The Offensive Line", plan.Assignments[0].AgentName);
        Assert.Equal([0], plan.Assignments[1].DependsOn);
    }

    [Fact]
    public async Task FencedJson_IsExtracted()
    {
        var plan = await Extract($"Here's the play:\n\n```json\n{ValidPlanJson}\n```\n\nReady when you are.");

        Assert.Equal(2, plan.Assignments.Count);
    }

    [Fact]
    public async Task UnlabelledFence_IsExtracted()
    {
        var plan = await Extract($"```\n{ValidPlanJson}\n```");

        Assert.Equal(2, plan.Assignments.Count);
    }

    [Fact]
    public async Task JsonSurroundedByProse_IsExtracted()
    {
        // No fence at all — extraction falls back to first-brace/last-brace.
        var plan = await Extract($"Alright team, here is the plan.\n{ValidPlanJson}\nLet's snap it.");

        Assert.Equal("Ship the login page", plan.Summary);
    }

    [Fact]
    public async Task SnakeCaseFieldsAreMapped()
    {
        var plan = await Extract(ValidPlanJson);

        var assignment = plan.Assignments[0];
        Assert.Equal("Worker", assignment.AgentRole);
        Assert.Equal("The Offensive Line", assignment.AgentName);
        Assert.Equal(1, assignment.Priority);
    }

    [Fact]
    public async Task EmptyAssignments_FallsThroughToTheProvider()
    {
        // A plan with no assignments is not usable, so the QB must be re-prompted.
        var parser = CreateParser();
        var provider = new FakeAgentProvider(ValidPlanJson);

        var plan = await parser.ParsePlanFromHuddleAsync(
            """{"summary":"nothing to do","assignments":[]}""",
            AgentTeam.CreateThePlaybook(),
            "add a login page",
            provider,
            workingDirectory: null);

        Assert.Single(provider.Calls);
        Assert.Equal(2, plan.Assignments.Count);
    }

    [Fact]
    public async Task ProseOnly_RePromptsAndUsesTheProvidersPlan()
    {
        var provider = new FakeAgentProvider(ValidPlanJson);

        var plan = await CreateParser().ParsePlanFromHuddleAsync(
            "I think we should probably start with the form and then review it.",
            AgentTeam.CreateThePlaybook(),
            "add a login page",
            provider,
            workingDirectory: null);

        Assert.Single(provider.Calls);
        Assert.Equal("Ship the login page", plan.Summary);
    }

    [Fact]
    public async Task ProviderAlsoFails_FallsBackToAGeneratedPlan()
    {
        // Both extraction attempts fail; the drive still has to have something to run.
        var provider = new FakeAgentProvider("I'm not going to give you JSON today.");

        var plan = await CreateParser().ParsePlanFromHuddleAsync(
            "no json here either",
            AgentTeam.CreateThePlaybook(),
            "add a login page",
            provider,
            workingDirectory: null);

        Assert.NotEmpty(plan.Assignments);
        Assert.Contains("Fallback", plan.Summary);
        // The fallback keeps the review gated behind the implementation work.
        var validator = plan.Assignments.Single(a => a.AgentRole == "Validator");
        Assert.NotEmpty(validator.DependsOn!);
    }

    [Fact]
    public async Task FallbackPlan_RunsWorkersAndTesterInParallel()
    {
        var provider = new FakeAgentProvider("nope");

        var plan = await CreateParser().ParsePlanFromHuddleAsync(
            "nope", AgentTeam.CreateThePlaybook(), "task", provider, null);

        var tester = plan.Assignments.Single(a => a.AgentRole == "Tester");
        var worker = plan.Assignments.First(a => a.AgentRole == "Worker");
        Assert.Empty(tester.DependsOn!);
        Assert.Empty(worker.DependsOn!);
    }

    [Fact]
    public async Task ModelOverrideAndEffort_ArePassedToTheProvider()
    {
        var provider = new FakeAgentProvider(ValidPlanJson);

        await CreateParser().ParsePlanFromHuddleAsync(
            "prose only", AgentTeam.CreateThePlaybook(), "task", provider,
            workingDirectory: "/tmp/workspace", modelOverride: "gpt-5.4", effort: "medium");

        var call = Assert.Single(provider.Calls);
        Assert.Equal("gpt-5.4", call.ModelId);
        Assert.Equal("medium", call.Effort);
        Assert.Equal("/tmp/workspace", call.WorkingDirectory);
    }

    [Fact]
    public async Task MalformedJsonInFence_DoesNotThrow()
    {
        var provider = new FakeAgentProvider(ValidPlanJson);

        var plan = await CreateParser().ParsePlanFromHuddleAsync(
            "```json\n{\"summary\": \"broken\", \"assignments\": [ {{{ ]\n```",
            AgentTeam.CreateThePlaybook(), "task", provider, null);

        Assert.Equal("Ship the login page", plan.Summary);
    }
}
