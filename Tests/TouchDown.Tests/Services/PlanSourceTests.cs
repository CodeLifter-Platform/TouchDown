using Microsoft.Extensions.Logging.Abstractions;
using TD.Models;
using TD.Services;
using TD.Services.Telemetry;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// How a plan was obtained is the strongest available signal of plan quality. A fallback
/// plan ignores the huddle conversation entirely, yet the drive executes identically — so
/// without carrying the source out of the parser, a quality collapse is invisible.
/// </summary>
public class PlanSourceTests
{
    private const string ValidPlanJson = """
        {"summary":"Ship it","assignments":[{"agent_role":"Worker","agent_name":"The Offensive Line","task":"build","depends_on":[],"priority":1}]}
        """;

    private static PlanParserService CreateParser() => new(NullLogger<PlanParserService>.Instance);

    private static Task<PlanResult> Parse(string huddle, FakeAgentProvider provider) =>
        CreateParser().ParsePlanFromHuddleAsync(
            huddle, AgentTeam.CreateThePlaybook(), "a task", provider, workingDirectory: null);

    [Fact]
    public async Task PlanFromHuddleJson_IsReportedAsHuddleJson()
    {
        // The provider throws if used, so this also proves no second model call was made.
        var result = await Parse(ValidPlanJson, new FakeAgentProvider());

        Assert.Equal(PlanSource.HuddleJson, result.Source);
    }

    [Fact]
    public async Task PlanFromFencedHuddleJson_IsStillHuddleJson()
    {
        var result = await Parse($"Here you go:\n```json\n{ValidPlanJson}\n```", new FakeAgentProvider());

        Assert.Equal(PlanSource.HuddleJson, result.Source);
    }

    [Fact]
    public async Task PlanRequiringARePrompt_IsReportedAsReprompt()
    {
        var result = await Parse("just some prose about the work", new FakeAgentProvider(ValidPlanJson));

        Assert.Equal(PlanSource.Reprompt, result.Source);
    }

    [Fact]
    public async Task PlanThatFellBack_IsReportedAsFallback()
    {
        // This is the quality cliff: the huddle is discarded and a mechanical plan runs instead.
        var result = await Parse("no json", new FakeAgentProvider("still no json"));

        Assert.Equal(PlanSource.Fallback, result.Source);
        Assert.NotEmpty(result.Plan.Assignments);
    }

    [Fact]
    public async Task PlanResultCarriesThePlanItself()
    {
        var result = await Parse(ValidPlanJson, new FakeAgentProvider());

        Assert.Equal("Ship it", result.Plan.Summary);
        Assert.Single(result.Plan.Assignments);
    }
}

/// <summary>
/// The scheduler silently breaks dependency cycles by dumping the remainder into a final
/// wave. That hides a Quarterback planning defect, so it is detected and reported.
/// </summary>
public class DependencyCycleDetectionTests
{
    private static bool HasCycle(int playCount, Dictionary<int, List<int>> deps) =>
        AgentOrchestrationService.HasDependencyCycle(playCount, deps);

    [Fact]
    public void AcyclicPlan_ReportsNoCycle()
    {
        Assert.False(HasCycle(3, new Dictionary<int, List<int>> { [0] = [], [1] = [0], [2] = [1] }));
    }

    [Fact]
    public void IndependentPlays_ReportNoCycle()
    {
        Assert.False(HasCycle(3, new Dictionary<int, List<int>> { [0] = [], [1] = [], [2] = [] }));
    }

    [Fact]
    public void MissingDependencyEntries_ReportNoCycle()
    {
        Assert.False(HasCycle(3, new Dictionary<int, List<int>> { [2] = [0] }));
    }

    [Fact]
    public void TwoPlaysDependingOnEachOther_ReportACycle()
    {
        Assert.True(HasCycle(2, new Dictionary<int, List<int>> { [0] = [1], [1] = [0] }));
    }

    [Fact]
    public void SelfDependency_ReportsACycle()
    {
        Assert.True(HasCycle(2, new Dictionary<int, List<int>> { [0] = [0], [1] = [] }));
    }

    [Fact]
    public void LongerCycle_ReportsACycle()
    {
        Assert.True(HasCycle(3, new Dictionary<int, List<int>> { [0] = [2], [1] = [0], [2] = [1] }));
    }

    [Fact]
    public void CycleAlongsideHealthyPlays_StillReportsACycle()
    {
        Assert.True(HasCycle(4, new Dictionary<int, List<int>> { [0] = [], [1] = [], [2] = [3], [3] = [2] }));
    }

    [Fact]
    public void DetectionAgreesWithTheSchedulerBreakingTheCycle()
    {
        // When a cycle exists the scheduler must still schedule everything exactly once,
        // and the detector must have flagged it.
        var deps = new Dictionary<int, List<int>> { [0] = [1], [1] = [0], [2] = [] };

        var waves = AgentOrchestrationService.BuildExecutionWaves(3, deps);

        Assert.True(HasCycle(3, deps));
        Assert.Equal([0, 1, 2], waves.SelectMany(w => w).Order());
    }

    [Fact]
    public void EmptyPlan_ReportsNoCycle()
    {
        Assert.False(HasCycle(0, []));
    }
}
