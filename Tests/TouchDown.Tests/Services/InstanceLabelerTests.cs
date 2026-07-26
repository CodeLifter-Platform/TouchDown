using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The orchestrator labels live agent cards and the monitor labels replayed plays using
/// this same function. If they disagree, a reloaded drive shows different agent names
/// than the one the user watched run.
/// </summary>
public class InstanceLabelerTests
{
    private static InstanceLabeler.PlayRef Play(
        int playId, int? memberId, string name, int orderIndex, int maxInstances = 1) =>
        new(playId, memberId, name, orderIndex, maxInstances);

    [Fact]
    public void SingleInstanceMember_KeepsPlainName()
    {
        var labels = InstanceLabeler.Label([Play(1, 10, "The Safety", 0)]);

        Assert.Equal("The Safety", labels[1]);
    }

    [Fact]
    public void FanOutMember_IsNumberedInOrderIndexOrder()
    {
        var labels = InstanceLabeler.Label([
            Play(3, 10, "The Offensive Line", orderIndex: 2, maxInstances: 4),
            Play(1, 10, "The Offensive Line", orderIndex: 0, maxInstances: 4),
            Play(2, 10, "The Offensive Line", orderIndex: 1, maxInstances: 4),
        ]);

        Assert.Equal("The Offensive Line #1", labels[1]);
        Assert.Equal("The Offensive Line #2", labels[2]);
        Assert.Equal("The Offensive Line #3", labels[3]);
    }

    [Fact]
    public void FanOutMemberWithASinglePlay_IsStillNumbered()
    {
        // MaxInstances > 1 marks the agent as a fan-out agent, so its card is numbered
        // even when the plan only gave it one slice.
        var labels = InstanceLabeler.Label([Play(1, 10, "The Defensive Line", 0, maxInstances: 4)]);

        Assert.Equal("The Defensive Line #1", labels[1]);
    }

    [Fact]
    public void NonFanOutMemberWithMultiplePlays_IsNumbered()
    {
        // Two plays for one agent still need distinguishable cards.
        var labels = InstanceLabeler.Label([
            Play(1, 20, "Special Teams", 0),
            Play(2, 20, "Special Teams", 1),
        ]);

        Assert.Equal("Special Teams #1", labels[1]);
        Assert.Equal("Special Teams #2", labels[2]);
    }

    [Fact]
    public void NumberingIsPerMember()
    {
        var labels = InstanceLabeler.Label([
            Play(1, 10, "The Offensive Line", 0, 4),
            Play(2, 11, "The Defensive Line", 1, 4),
            Play(3, 10, "The Offensive Line", 2, 4),
            Play(4, 11, "The Defensive Line", 3, 4),
        ]);

        Assert.Equal("The Offensive Line #1", labels[1]);
        Assert.Equal("The Defensive Line #1", labels[2]);
        Assert.Equal("The Offensive Line #2", labels[3]);
        Assert.Equal("The Defensive Line #2", labels[4]);
    }

    [Fact]
    public void UnassignedPlays_ShareTheUnassignedBucket()
    {
        var labels = InstanceLabeler.Label([
            Play(1, null, "Unknown", 0),
            Play(2, null, "Unknown", 1),
        ]);

        Assert.Equal("Unknown #1", labels[1]);
        Assert.Equal("Unknown #2", labels[2]);
    }

    [Fact]
    public void TiedOrderIndex_FallsBackToPlayIdForStability()
    {
        // Ordering must be deterministic or live and replayed labels can diverge.
        var labels = InstanceLabeler.Label([
            Play(7, 10, "The Offensive Line", orderIndex: 0, maxInstances: 4),
            Play(2, 10, "The Offensive Line", orderIndex: 0, maxInstances: 4),
        ]);

        Assert.Equal("The Offensive Line #1", labels[2]);
        Assert.Equal("The Offensive Line #2", labels[7]);
    }

    [Fact]
    public void LabellingIsOrderIndependent()
    {
        var plays = new[]
        {
            Play(1, 10, "The Offensive Line", 0, 4),
            Play(2, 10, "The Offensive Line", 1, 4),
            Play(3, 11, "The Safety", 2),
        };

        var forward = InstanceLabeler.Label(plays);
        var reversed = InstanceLabeler.Label(plays.Reverse());

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(InstanceLabeler.Label([]));
    }
}
