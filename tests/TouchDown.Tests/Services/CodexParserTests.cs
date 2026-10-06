using TD.Models;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The Codex CLI's stdout, one line at a time: structured JSON events with a "type"
/// discriminator where the CLI emits them, plain text where it does not.
///
/// The fixture is reconstructed from the event shapes <see cref="CodexProvider.ParseLine"/>
/// handles (text, tool_call, result, error), not captured from a live <c>codex exec</c>
/// run; a captured transcript should replace it when one is available. Note that the
/// provider does not pass <c>--json</c>, so in practice most output arrives as plain text
/// and takes the fallback path pinned below.
/// </summary>
public class CodexParserTests
{
    private const string Fixture = """
        {"type":"thread.started","thread_id":"thr_1"}
        {"type":"text","content":"Reading the repository layout."}
        {"type":"tool_call","name":"shell","arguments":{"command":["ls","-la"]}}
        {"type":"text","content":"Added the login page."}
        {"type":"result","content":"Added the login page."}
        """;

    private static List<AgentStreamChunk> Parse(string lines) =>
        lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CodexProvider.ParseLine)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

    [Fact]
    public void Structured_text_events_become_text_deltas()
    {
        var chunks = Parse(Fixture);

        Assert.Equal(["Reading the repository layout.", "Added the login page."],
            chunks.Where(c => c.TextDelta != null).Select(c => c.TextDelta));
    }

    [Fact]
    public void A_tool_call_names_the_tool()
    {
        var chunks = Parse(Fixture);

        Assert.Equal(["shell"], chunks.Where(c => c.ToolName != null).Select(c => c.ToolName));
    }

    [Fact]
    public void The_result_event_completes_the_run_and_unknown_events_are_ignored()
    {
        var chunks = Parse(Fixture);

        var result = Assert.Single(chunks, c => c.IsComplete);
        Assert.Equal("Added the login page.", result.Result);
        Assert.False(result.IsError);
        Assert.Equal(4, chunks.Count);
    }

    [Fact]
    public void An_error_event_is_reported_with_its_message()
    {
        var chunk = CodexProvider.ParseLine("""{"type":"error","message":"rate limited"}""");

        Assert.NotNull(chunk);
        Assert.True(chunk.IsComplete);
        Assert.True(chunk.IsError);
        Assert.Equal("rate limited", chunk.Result);
    }

    [Fact]
    public void A_plain_text_line_is_a_text_delta_with_its_newline_restored()
    {
        var chunk = CodexProvider.ParseLine("Reading README.md");

        Assert.Equal("Reading README.md\n", chunk?.TextDelta);
    }

    [Fact]
    public void Json_without_a_type_is_passed_through_as_text()
    {
        var chunk = CodexProvider.ParseLine("""{"message":"hello"}""");

        Assert.Equal("""{"message":"hello"}""", chunk?.TextDelta);
    }

    [Fact]
    public void A_truncated_json_line_is_treated_as_text_rather_than_thrown()
    {
        var chunk = CodexProvider.ParseLine("""{"type":"text","content":"cut off""");

        Assert.NotNull(chunk);
        Assert.StartsWith("{\"type\":\"text\"", chunk.TextDelta);
    }
}
