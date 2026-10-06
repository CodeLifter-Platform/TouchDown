using TD.Models;
using TD.Services;

namespace TouchDown.Tests.Services;

/// <summary>
/// The command lines the providers hand to the CLIs. A missing flag here means a run that
/// silently uses the wrong model, ignores the effort setting, or blocks on a permission
/// prompt nobody can answer. Nothing is started; only the argument list is inspected.
/// </summary>
public class ProviderArgumentTests
{
    private static List<string> ClaudeArgs(ClaudeRunOptions options) =>
        ClaudeStreamingService.CreateProcess(options).StartInfo.ArgumentList.ToList();

    private static List<string> CodexArgs(AgentContext context) =>
        CodexProvider.BuildProcess(context).StartInfo.ArgumentList.ToList();

    private static int IndexAfter(List<string> args, string flag)
    {
        var i = args.IndexOf(flag);
        Assert.True(i >= 0, $"expected flag {flag} in: {string.Join(' ', args)}");
        return i + 1;
    }

    // ── Claude ──────────────────────────────────────────────────────────────

    [Fact]
    public void Claude_always_streams_json_with_partial_messages_for_the_chosen_model()
    {
        var args = ClaudeArgs(new ClaudeRunOptions { ModelId = "example-model", Prompt = "hi" });

        Assert.Contains("--print", args);
        Assert.Contains("--verbose", args);
        Assert.Contains("--include-partial-messages", args);
        Assert.Equal("stream-json", args[IndexAfter(args, "--output-format")]);
        Assert.Equal("example-model", args[IndexAfter(args, "--model")]);
    }

    [Fact]
    public void Claude_omits_the_effort_flag_when_no_effort_is_set()
    {
        var args = ClaudeArgs(new ClaudeRunOptions { ModelId = "m", Prompt = "hi", Effort = null });

        Assert.DoesNotContain("--effort", args);
    }

    [Fact]
    public void Claude_passes_the_effort_when_set()
    {
        var args = ClaudeArgs(new ClaudeRunOptions { ModelId = "m", Prompt = "hi", Effort = "high" });

        Assert.Equal("high", args[IndexAfter(args, "--effort")]);
    }

    [Fact]
    public void Claude_tool_lists_and_permission_flags_follow_the_options()
    {
        var args = ClaudeArgs(new ClaudeRunOptions
        {
            ModelId = "m",
            Prompt = "hi",
            SystemPrompt = "be terse",
            AppendSystemPrompt = "and kind",
            AllowedTools = ["Read", "Grep"],
            DisallowedTools = ["Task"],
            DangerouslySkipPermissions = true,
            MaxBudgetUsd = 1.5,
        });

        Assert.Equal("be terse", args[IndexAfter(args, "--system-prompt")]);
        Assert.Equal("and kind", args[IndexAfter(args, "--append-system-prompt")]);
        var allowed = IndexAfter(args, "--allowedTools");
        Assert.Equal(["Read", "Grep"], args.GetRange(allowed, 2));
        Assert.Equal("Task", args[IndexAfter(args, "--disallowedTools")]);
        Assert.Contains("--dangerously-skip-permissions", args);
        Assert.Equal("1.50", args[IndexAfter(args, "--max-budget-usd")]);
    }

    [Fact]
    public void Claude_leaves_optional_flags_out_when_their_options_are_unset()
    {
        var args = ClaudeArgs(new ClaudeRunOptions { ModelId = "m", Prompt = "hi" });

        Assert.DoesNotContain("--system-prompt", args);
        Assert.DoesNotContain("--allowedTools", args);
        Assert.DoesNotContain("--disallowedTools", args);
        Assert.DoesNotContain("--dangerously-skip-permissions", args);
        Assert.DoesNotContain("--max-budget-usd", args);
    }

    [Fact]
    public void Claude_runs_in_the_working_directory_with_the_prompt_on_stdin_and_a_clear_nesting_guard()
    {
        var process = ClaudeStreamingService.CreateProcess(new ClaudeRunOptions
        {
            ModelId = "m", Prompt = "hi", WorkingDirectory = "/tmp/work"
        });

        Assert.Equal("/tmp/work", process.StartInfo.WorkingDirectory);
        Assert.True(process.StartInfo.RedirectStandardInput);
        Assert.Equal("", process.StartInfo.Environment["CLAUDECODE"]);
        Assert.DoesNotContain("hi", process.StartInfo.ArgumentList);
    }

    // ── Codex ───────────────────────────────────────────────────────────────

    [Fact]
    public void Codex_runs_non_interactively_on_the_chosen_model()
    {
        var args = CodexArgs(new AgentContext { ModelId = "gpt-5.4", Prompt = "hi" });

        Assert.Equal("exec", args[0]);
        Assert.Contains("--full-auto", args);
        Assert.Equal("gpt-5.4", args[IndexAfter(args, "--model")]);
    }

    [Fact]
    public void Codex_omits_the_reasoning_override_when_no_effort_is_set()
    {
        var args = CodexArgs(new AgentContext { ModelId = "m", Prompt = "hi" });

        Assert.DoesNotContain("-c", args);
    }

    [Fact]
    public void Codex_passes_the_effort_as_a_config_override()
    {
        var args = CodexArgs(new AgentContext { ModelId = "m", Prompt = "hi", Effort = "high" });

        Assert.Equal("model_reasoning_effort=high", args[IndexAfter(args, "-c")]);
    }

    [Fact]
    public void Codex_changes_into_the_working_directory_when_one_is_given()
    {
        var withDir = CodexProvider.BuildProcess(new AgentContext { ModelId = "m", Prompt = "hi", WorkingDirectory = "/tmp/work" });
        var without = CodexArgs(new AgentContext { ModelId = "m", Prompt = "hi" });

        Assert.Equal("/tmp/work", withDir.StartInfo.ArgumentList[IndexAfter(withDir.StartInfo.ArgumentList.ToList(), "--cd")]);
        Assert.Equal("/tmp/work", withDir.StartInfo.WorkingDirectory);
        Assert.DoesNotContain("--cd", without);
    }

    [Fact]
    public void Codex_folds_the_system_prompt_into_the_prompt_argument()
    {
        var bare = CodexArgs(new AgentContext { ModelId = "m", Prompt = "do it" });
        var withSystem = CodexArgs(new AgentContext { ModelId = "m", Prompt = "do it", SystemPrompt = "be terse" });

        Assert.Equal("do it", bare[^1]);
        Assert.Equal("[System instructions]\nbe terse\n\n[Task]\ndo it", withSystem[^1]);
    }
}
