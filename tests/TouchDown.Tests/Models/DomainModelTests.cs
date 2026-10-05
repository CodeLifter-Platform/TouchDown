using TD.Models;

namespace TouchDown.Tests.Models;

public class DriveDisplayNameTests
{
    private static Drive DriveWith(string? name, string task) =>
        new() { Name = name, TaskDescription = task };

    [Fact]
    public void PrefersTheExplicitName()
    {
        Assert.Equal("Login rework", DriveWith("Login rework", "some long task").DisplayName);
    }

    [Fact]
    public void FallsBackToTheTaskWhenUnnamed()
    {
        Assert.Equal("add a login page", DriveWith(null, "add a login page").DisplayName);
    }

    [Fact]
    public void WhitespaceNameIsTreatedAsUnset()
    {
        Assert.Equal("add a login page", DriveWith("   ", "add a login page").DisplayName);
    }

    [Fact]
    public void LongTaskIsTruncatedWithAnEllipsis()
    {
        var task = new string('x', 100);

        var display = DriveWith(null, task).DisplayName;

        // 57 characters of task plus the ellipsis.
        Assert.Equal(58, display.Length);
        Assert.EndsWith("…", display);
    }

    [Fact]
    public void TaskOfExactlySixtyCharsIsNotTruncated()
    {
        var task = new string('x', 60);

        Assert.Equal(task, DriveWith(null, task).DisplayName);
    }

    [Fact]
    public void FallsBackToTheDriveIdWhenThereIsNoNameOrTask()
    {
        var drive = new Drive { TaskDescription = "" };

        Assert.Equal(drive.DriveId, drive.DisplayName);
    }

    [Fact]
    public void DriveIdIsATwelveCharacterSlug()
    {
        var drive = new Drive();

        Assert.Equal(12, drive.DriveId.Length);
        Assert.DoesNotContain('-', drive.DriveId);
    }

    [Fact]
    public void NewDrivesDefaultToPending()
    {
        var drive = new Drive();

        Assert.Equal(DriveStatus.Pending, drive.Status);
        Assert.Equal(2, drive.MaxParallelism);
        Assert.Equal(AgentEffort.High, drive.Effort);
        Assert.True(drive.OverrideTeamConfig);
    }
}

public class AgentTeamTests
{
    [Fact]
    public void ThePlaybook_HasExactlyOneLeader()
    {
        var team = AgentTeam.CreateThePlaybook();

        Assert.Single(team.Members, m => m.IsLeader);
        Assert.Equal("The Quarterback", team.GetLeader()!.Name);
    }

    [Fact]
    public void ThePlaybook_CoversEveryRole()
    {
        var roles = AgentTeam.CreateThePlaybook().Members.Select(m => m.Role).ToHashSet();

        Assert.Equal(Enum.GetValues<AgentRole>().ToHashSet(), roles);
    }

    [Fact]
    public void ThePlaybook_MarksTheLinesAsFanOutAgents()
    {
        var team = AgentTeam.CreateThePlaybook();

        Assert.Equal(4, team.Members.Single(m => m.Name == "The Offensive Line").MaxInstances);
        Assert.Equal(4, team.Members.Single(m => m.Name == "The Defensive Line").MaxInstances);
        Assert.Equal(1, team.Members.Single(m => m.Name == "The Safety").MaxInstances);
    }

    [Fact]
    public void ThePlaybook_GivesEveryMemberASystemPrompt()
    {
        var team = AgentTeam.CreateThePlaybook();

        Assert.All(team.Members, m => Assert.False(string.IsNullOrWhiteSpace(m.SystemPrompt)));
    }

    [Fact]
    public void GetLeader_ReturnsNullWhenThereIsNone()
    {
        var team = new AgentTeam
        {
            Name = "Leaderless",
            Members = [new AgentMember { Name = "Solo", Role = AgentRole.Worker }]
        };

        Assert.Null(team.GetLeader());
    }

    [Fact]
    public void RosterPrompt_NamesEveryMember()
    {
        var team = AgentTeam.CreateThePlaybook();

        var prompt = team.BuildRosterPrompt();

        foreach (var member in team.Members)
            Assert.Contains(member.Name, prompt);
    }

    [Fact]
    public void RosterPrompt_AnnotatesFanOutAgents()
    {
        var prompt = AgentTeam.CreateThePlaybook().BuildRosterPrompt();

        Assert.Contains("fans out into up to 4 parallel instances", prompt);
    }

    [Fact]
    public void RosterPrompt_ForbidsSpeakingForTeammates()
    {
        // This instruction is what stops an agent inventing a teammate's roll-call answer.
        var prompt = AgentTeam.CreateThePlaybook().BuildRosterPrompt();

        Assert.Contains("You speak ONLY for yourself", prompt);
        Assert.Contains("Never fabricate", prompt);
    }

    [Fact]
    public void RosterPrompt_IncludesTheTeamName()
    {
        var team = new AgentTeam
        {
            Name = "Special Squad",
            Members = [new AgentMember { Name = "Solo", Role = AgentRole.Worker }]
        };

        Assert.Contains("Special Squad", team.BuildRosterPrompt());
    }
}

public class EffortAndModelMappingTests
{
    [Theory]
    [InlineData(AgentEffort.Low, "low")]
    [InlineData(AgentEffort.Medium, "medium")]
    [InlineData(AgentEffort.High, "high")]
    [InlineData(AgentEffort.XHigh, "xhigh")]
    [InlineData(AgentEffort.Max, "max")]
    public void ToCliValue_MapsEveryLevel(AgentEffort effort, string expected)
    {
        Assert.Equal(expected, effort.ToCliValue());
    }

    [Fact]
    public void ToDisplayName_ExpandsXHigh()
    {
        Assert.Equal("Extra High", AgentEffort.XHigh.ToDisplayName());
        Assert.Equal("Low", AgentEffort.Low.ToDisplayName());
    }

    [Fact]
    public void EveryClaudeModelMapsToANonEmptyId()
    {
        foreach (var model in Enum.GetValues<ClaudeModel>())
        {
            Assert.False(string.IsNullOrWhiteSpace(model.ToModelId()));
            Assert.False(string.IsNullOrWhiteSpace(model.ToDisplayName()));
        }
    }

    [Fact]
    public void ClaudeModelIdsAreDistinct()
    {
        var ids = Enum.GetValues<ClaudeModel>().Select(m => m.ToModelId()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}

public class PlayTests
{
    [Fact]
    public void NewPlaysStartPending()
    {
        var play = new Play { Description = "do it" };

        Assert.Equal(PlayStatus.Pending, play.Status);
        Assert.Null(play.StartedAt);
        Assert.Null(play.CompletedAt);
    }

    [Fact]
    public void MemberIsALeaderOnlyForTheLeaderRole()
    {
        Assert.True(new AgentMember { Role = AgentRole.Leader }.IsLeader);
        Assert.False(new AgentMember { Role = AgentRole.Worker }.IsLeader);
    }

    [Fact]
    public void MembersDefaultToASingleInstance()
    {
        Assert.Equal(1, new AgentMember { Name = "x" }.MaxInstances);
    }
}
