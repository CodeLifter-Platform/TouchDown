using Microsoft.Extensions.Logging.Abstractions;
using TD.Models;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The plan parser turns a free-text huddle into structured assignments. LLM output is
/// unreliable in shape, so extraction has to cope with fenced JSON, prose around JSON,
/// and outright garbage.
/// </summary>
public class PlanParserServiceTests
{
    private static PlanParserService CreateParser() =>
        new(NullLogger<PlanParserService>.Instance);

    private static AgentTeam Playbook()
    {
        var team = AgentTeam.CreateThePlaybook();
        // Give members ids the way the database would.
        for (var i = 0; i < team.Members.Count; i++)
            team.Members[i].Id = i + 1;
        return team;
    }

    // ── ConvertPlanToPlays ──────────────────────────────────────────────────

    [Fact]
    public void ConvertPlanToPlays_MatchesMembersByName()
    {
        var plan = new QuarterbackPlan
        {
            Summary = "s",
            Assignments =
            [
                new() { AgentName = "The Offensive Line", AgentRole = "Worker", Task = "build it" },
                new() { AgentName = "The Safety", AgentRole = "Validator", Task = "review it" },
            ]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal(2, plays.Count);
        Assert.Equal("The Offensive Line", plays[0].AssignedMember!.Name);
        Assert.Equal("The Safety", plays[1].AssignedMember!.Name);
    }

    [Fact]
    public void ConvertPlanToPlays_MatchesByNameCaseInsensitively()
    {
        var plan = new QuarterbackPlan
        {
            Assignments = [new() { AgentName = "the offensive LINE", AgentRole = "Worker", Task = "t" }]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal("The Offensive Line", Assert.Single(plays).AssignedMember!.Name);
    }

    [Fact]
    public void ConvertPlanToPlays_FallsBackToRoleWhenNameIsUnknown()
    {
        var plan = new QuarterbackPlan
        {
            Assignments = [new() { AgentName = "Nobody On This Team", AgentRole = "Tester", Task = "test it" }]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal(AgentRole.Tester, Assert.Single(plays).AssignedMember!.Role);
    }

    [Fact]
    public void ConvertPlanToPlays_SkipsAssignmentsThatMatchNothing()
    {
        var plan = new QuarterbackPlan
        {
            Assignments =
            [
                new() { AgentName = "Ghost", AgentRole = "Astronaut", Task = "unassignable" },
                new() { AgentName = "The Safety", AgentRole = "Validator", Task = "real work" },
            ]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal("real work", Assert.Single(plays).Description);
    }

    [Fact]
    public void ConvertPlanToPlays_PreservesPlanOrder()
    {
        var plan = new QuarterbackPlan
        {
            Assignments =
            [
                new() { AgentName = "The Scout", AgentRole = "Researcher", Task = "first" },
                new() { AgentName = "The Offensive Line", AgentRole = "Worker", Task = "second" },
                new() { AgentName = "The Safety", AgentRole = "Validator", Task = "third" },
            ]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal([0, 1, 2], plays.Select(p => p.OrderIndex));
        Assert.Equal(["first", "second", "third"], plays.Select(p => p.Description));
    }

    [Fact]
    public void ConvertPlanToPlays_EmptyPlan_ProducesNoPlays()
    {
        var plays = CreateParser().ConvertPlanToPlays(new QuarterbackPlan(), Playbook());

        Assert.Empty(plays);
    }

    [Fact]
    public void ConvertPlanToPlays_RepeatedAgent_BecomesSeparateFanOutPlays()
    {
        var plan = new QuarterbackPlan
        {
            Assignments =
            [
                new() { AgentName = "The Offensive Line", AgentRole = "Worker", Task = "slice A" },
                new() { AgentName = "The Offensive Line", AgentRole = "Worker", Task = "slice B" },
            ]
        };

        var plays = CreateParser().ConvertPlanToPlays(plan, Playbook());

        Assert.Equal(2, plays.Count);
        Assert.All(plays, p => Assert.Equal("The Offensive Line", p.AssignedMember!.Name));
    }
}
