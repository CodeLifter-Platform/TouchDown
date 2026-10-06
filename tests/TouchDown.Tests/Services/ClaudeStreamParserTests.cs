using Microsoft.Extensions.Logging.Abstractions;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The Claude CLI's <c>--output-format stream-json --include-partial-messages</c> output,
/// one event per line, mapped to the chunks the orchestrator and huddle consume.
///
/// The fixture below is reconstructed from the shape the <c>ClaudeStreamEvent</c> model
/// parses (the stream_event envelope, content_block_* events, the terminal result event),
/// not captured from a live CLI run; a captured transcript should replace it when one is
/// available. What it pins is that a run produces its text, its tool names and its final
/// result/cost/duration, and that a non-JSON line is skipped rather than ending the run.
/// </summary>
public class ClaudeStreamParserTests
{
    private const string Fixture = """
        {"type":"system","subtype":"init","session_id":"abc123","tools":["Read","Edit","Bash"],"model":"example-model"}
        {"type":"stream_event","event":{"type":"message_start","message":{"id":"msg_1","role":"assistant","content":[]}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Looking at "}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"the repo."}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_stop","index":0},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_1","name":"Read","input":{}}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"file_path\":\"README.md\"}"}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_stop","index":1},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"message_delta","delta":{"type":"message_delta","stop_reason":"tool_use"},"usage":{"output_tokens":42}},"session_id":"abc123"}
        {"type":"assistant","message":{"id":"msg_1","role":"assistant","content":[{"type":"text","text":"Looking at the repo."},{"type":"tool_use","id":"toolu_1","name":"Read","input":{"file_path":"README.md"}}]},"session_id":"abc123"}
        {"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"# TouchDown"}]},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Done."}},"session_id":"abc123"}
        {"type":"stream_event","event":{"type":"content_block_stop","index":0},"session_id":"abc123"}
        {"type":"result","subtype":"success","is_error":false,"duration_ms":15234,"duration_api_ms":14001,"num_turns":3,"result":"Done.","session_id":"abc123","total_cost_usd":0.0421}
        """;

    private static List<ClaudeStreamChunk> Parse(string lines) =>
        lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ClaudeStreamingService.ParseLine(line, NullLogger.Instance))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

    [Fact]
    public void A_run_yields_its_text_in_order()
    {
        var chunks = Parse(Fixture);

        Assert.Equal(["Looking at ", "the repo.", "Done."],
            chunks.Where(c => c.TextDelta != null).Select(c => c.TextDelta));
    }

    [Fact]
    public void A_tool_use_block_yields_the_tool_name_once()
    {
        var chunks = Parse(Fixture);

        Assert.Equal(["Read"], chunks.Where(c => c.ToolName != null).Select(c => c.ToolName));
    }

    [Fact]
    public void The_result_event_closes_the_run_with_cost_duration_and_turns()
    {
        var chunks = Parse(Fixture);

        var result = Assert.Single(chunks, c => c.IsComplete);
        Assert.Same(result, chunks[^1]);
        Assert.False(result.IsError);
        Assert.Equal("Done.", result.Result);
        Assert.Equal(0.0421, result.CostUsd);
        Assert.Equal(15234, result.DurationMs);
        Assert.Equal(3, result.NumTurns);
    }

    [Fact]
    public void Bookkeeping_events_produce_no_chunks()
    {
        // system, message_start, content_block_stop, message_delta, the assistant/user
        // echoes and the input_json_delta all carry nothing the UI shows.
        var chunks = Parse(Fixture);

        Assert.Equal(5, chunks.Count);
    }

    [Fact]
    public void An_error_result_is_reported_as_an_error_with_the_message()
    {
        var chunk = ClaudeStreamingService.ParseLine(
            """{"type":"result","subtype":"error_during_execution","is_error":true,"result":"Credit balance is too low","duration_ms":120,"num_turns":1,"total_cost_usd":0}""",
            NullLogger.Instance);

        Assert.NotNull(chunk);
        Assert.True(chunk.IsComplete);
        Assert.True(chunk.IsError);
        Assert.Equal("Credit balance is too low", chunk.Result);
    }

    [Fact]
    public void Events_without_the_partial_message_envelope_still_map()
    {
        // The shape without --include-partial-messages, which older builds emitted at top level.
        var text = ClaudeStreamingService.ParseLine(
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"hi"}}""", NullLogger.Instance);
        var tool = ClaudeStreamingService.ParseLine(
            """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","name":"Bash"}}""", NullLogger.Instance);

        Assert.Equal("hi", text?.TextDelta);
        Assert.Equal("Bash", tool?.ToolName);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"type\":")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    public void A_line_that_is_not_an_event_is_skipped_without_throwing(string line)
    {
        var chunk = ClaudeStreamingService.ParseLine(line, NullLogger.Instance);

        Assert.Null(chunk);
    }

    [Fact]
    public void A_text_block_start_is_not_mistaken_for_a_tool()
    {
        var chunk = ClaudeStreamingService.ParseLine(
            """{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}}""",
            NullLogger.Instance);

        Assert.Null(chunk);
    }
}
