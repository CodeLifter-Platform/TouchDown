using TD.Models;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The wave builder is the drive scheduler: it decides what runs in parallel and what
/// waits. Getting it wrong either serialises everything or runs work before its
/// dependencies exist.
/// </summary>
public class ExecutionWaveTests
{
    private static List<List<int>> Build(int playCount, Dictionary<int, List<int>> deps) =>
        AgentOrchestrationService.BuildExecutionWaves(playCount, deps);

    [Fact]
    public void NoDependencies_AllPlaysRunInOneWave()
    {
        var waves = Build(4, new Dictionary<int, List<int>>
        {
            [0] = [], [1] = [], [2] = [], [3] = []
        });

        Assert.Single(waves);
        Assert.Equal([0, 1, 2, 3], waves[0].Order());
    }

    [Fact]
    public void LinearChain_ProducesOneWavePerStep()
    {
        var waves = Build(3, new Dictionary<int, List<int>>
        {
            [0] = [], [1] = [0], [2] = [1]
        });

        Assert.Equal(3, waves.Count);
        Assert.Equal([0], waves[0]);
        Assert.Equal([1], waves[1]);
        Assert.Equal([2], waves[2]);
    }

    [Fact]
    public void FanOutThenJoin_GroupsIndependentWorkTogether()
    {
        // The shape the Quarterback usually produces: implementers in parallel,
        // then the Safety reviewing everything.
        var waves = Build(4, new Dictionary<int, List<int>>
        {
            [0] = [],          // Scout
            [1] = [0],         // Offensive Line #1
            [2] = [0],         // Offensive Line #2
            [3] = [1, 2]       // Safety
        });

        Assert.Equal(3, waves.Count);
        Assert.Equal([0], waves[0]);
        Assert.Equal([1, 2], waves[1].Order());
        Assert.Equal([3], waves[2]);
    }

    [Fact]
    public void MissingDependencyEntry_TreatedAsNoDependencies()
    {
        // Assignments can arrive without a depends_on key at all.
        var waves = Build(3, new Dictionary<int, List<int>> { [2] = [0] });

        Assert.Equal(2, waves.Count);
        Assert.Equal([0, 1], waves[0].Order());
        Assert.Equal([2], waves[1]);
    }

    [Fact]
    public void CircularDependency_DoesNotHang_AndSchedulesEveryPlay()
    {
        // A QB-authored cycle must not deadlock the drive.
        var waves = Build(3, new Dictionary<int, List<int>>
        {
            [0] = [1], [1] = [0], [2] = []
        });

        var scheduled = waves.SelectMany(w => w).Order().ToList();
        Assert.Equal([0, 1, 2], scheduled);
    }

    [Fact]
    public void SelfDependency_IsBrokenRatherThanDeadlocked()
    {
        var waves = Build(2, new Dictionary<int, List<int>> { [0] = [0], [1] = [] });

        var scheduled = waves.SelectMany(w => w).Order().ToList();
        Assert.Equal([0, 1], scheduled);
    }

    [Fact]
    public void EveryPlayIsScheduledExactlyOnce()
    {
        var waves = Build(6, new Dictionary<int, List<int>>
        {
            [0] = [], [1] = [0], [2] = [0], [3] = [1, 2], [4] = [], [5] = [4]
        });

        var scheduled = waves.SelectMany(w => w).ToList();
        Assert.Equal(6, scheduled.Count);
        Assert.Equal(6, scheduled.Distinct().Count());
    }

    [Fact]
    public void DependenciesAlwaysLandInAnEarlierWave()
    {
        var deps = new Dictionary<int, List<int>>
        {
            [0] = [], [1] = [0], [2] = [1], [3] = [0], [4] = [2, 3]
        };

        var waves = Build(5, deps);

        var waveOf = waves
            .SelectMany((wave, index) => wave.Select(play => (play, index)))
            .ToDictionary(x => x.play, x => x.index);

        foreach (var (play, dependencies) in deps)
            foreach (var dependency in dependencies)
                Assert.True(waveOf[dependency] < waveOf[play],
                    $"play {play} ran in wave {waveOf[play]} but depends on {dependency} in wave {waveOf[dependency]}");
    }

    [Fact]
    public void ZeroPlays_ProducesNoWaves()
    {
        Assert.Empty(Build(0, []));
    }

    // ── Role tool allow-lists ───────────────────────────────────────────────

    [Fact]
    public void Researcher_GetsWebToolsAndNoWriteAccess()
    {
        var tools = AgentOrchestrationService.GetToolsForRole(AgentRole.Researcher);

        Assert.Contains("WebSearch", tools);
        Assert.Contains("WebFetch", tools);
        Assert.DoesNotContain("Write", tools);
        Assert.DoesNotContain("Edit", tools);
        Assert.DoesNotContain("Bash", tools);
    }

    [Fact]
    public void Validator_IsReadOnly()
    {
        // The Safety reviews code; it must not be able to rewrite what it is reviewing.
        var tools = AgentOrchestrationService.GetToolsForRole(AgentRole.Validator);

        Assert.DoesNotContain("Write", tools);
        Assert.DoesNotContain("Edit", tools);
        Assert.Contains("Read", tools);
    }

    [Theory]
    [InlineData(AgentRole.Worker)]
    [InlineData(AgentRole.Tester)]
    [InlineData(AgentRole.DevOps)]
    public void ImplementingRoles_CanEditAndRunCommands(AgentRole role)
    {
        var tools = AgentOrchestrationService.GetToolsForRole(role);

        Assert.Contains("Edit", tools);
        Assert.Contains("Write", tools);
        Assert.Contains("Bash", tools);
    }
}
