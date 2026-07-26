using System.Text.Json;
using TD.Models;

namespace TouchDown.Tests.Models;

/// <summary>
/// The CLI's terminal "result" event carries the run's cost, duration and turn count.
/// These were previously dropped — cost was read from the wrong field name, and duration
/// and turns were never read at all — so the UI showed no cost and no timings.
/// </summary>
public class ClaudeStreamEventTests
{
    private static ClaudeStreamEvent Parse(string json) =>
        JsonSerializer.Deserialize<ClaudeStreamEvent>(json)!;

    [Fact]
    public void ResultEvent_ReadsTotalCostUsd()
    {
        // Regression: the CLI reports "total_cost_usd"; only "cost_usd" was mapped, so
        // every run came back with a null cost.
        var evt = Parse("""
            {"type":"result","subtype":"success","is_error":false,
             "total_cost_usd":0.0421,"duration_ms":15234,"num_turns":7,"result":"done"}
            """);

        Assert.Equal(0.0421, evt.ResolvedCostUsd);
        Assert.Equal(15234, evt.DurationMs);
        Assert.Equal(7, evt.NumTurns);
        Assert.False(evt.IsError);
        Assert.Equal("done", evt.Result);
    }

    [Fact]
    public void ResultEvent_StillReadsLegacyCostUsd()
    {
        var evt = Parse("""{"type":"result","cost_usd":0.15}""");

        Assert.Equal(0.15, evt.ResolvedCostUsd);
    }

    [Fact]
    public void ResultEvent_PrefersTotalCostWhenBothPresent()
    {
        var evt = Parse("""{"type":"result","cost_usd":0.15,"total_cost_usd":0.99}""");

        Assert.Equal(0.99, evt.ResolvedCostUsd);
    }

    [Fact]
    public void ResultEvent_NoCostFields_ResolvesToNull()
    {
        var evt = Parse("""{"type":"result"}""");

        Assert.Null(evt.ResolvedCostUsd);
    }

    [Fact]
    public void PartialMessageEnvelope_CarriesNestedEvent()
    {
        // --include-partial-messages wraps the real event under "event".
        var evt = Parse("""
            {"type":"stream_event",
             "event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"hello"}}}
            """);

        Assert.Equal("stream_event", evt.Type);
        Assert.NotNull(evt.Event);
        Assert.Equal("content_block_delta", evt.Event!.Type);
        Assert.Equal("hello", evt.Event.Delta?.Text);
    }

    [Fact]
    public void ToolUseBlock_CarriesToolName()
    {
        var evt = Parse("""
            {"type":"content_block_start","content_block":{"type":"tool_use","name":"Bash","id":"tu_1"}}
            """);

        Assert.Equal("tool_use", evt.ContentBlock?.Type);
        Assert.Equal("Bash", evt.ContentBlock?.Name);
    }

    [Fact]
    public void ErrorResult_IsFlagged()
    {
        var evt = Parse("""{"type":"result","is_error":true,"result":"credit balance too low"}""");

        Assert.True(evt.IsError);
        Assert.Equal("credit balance too low", evt.Result);
    }

    [Fact]
    public void UnknownFields_DoNotThrow()
    {
        // The CLI adds fields over time; parsing must tolerate them.
        var evt = Parse("""{"type":"result","some_new_field":{"nested":true},"total_cost_usd":1.0}""");

        Assert.Equal("result", evt.Type);
        Assert.Equal(1.0, evt.ResolvedCostUsd);
    }
}
