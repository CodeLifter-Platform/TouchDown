namespace TD.Services.Telemetry;

/// <summary>
/// All telemetry event name constants.
/// Callers use these instead of magic strings to prevent typos and allow easy refactoring.
/// HARD RULE: No value passed with these events may contain code, paths, task text, or agent output.
/// </summary>
public static class TelemetryEvent
{
    // ── Usage ────────────────────────────────────────────────────────────────
    /// <summary>A wizard step was completed (prop: step_index, step_name).</summary>
    public const string WizardStepCompleted = "wizard.step_completed";

    /// <summary>A preset team was selected (prop: team_id — no team name).</summary>
    public const string TeamPresetSelected = "team.preset_selected";

    /// <summary>A custom team was created (prop: member_count).</summary>
    public const string TeamCustomCreated = "team.custom_created";

    /// <summary>A Huddle session was started.</summary>
    public const string HuddleStarted = "huddle.started";

    /// <summary>A Huddle plan was approved and the ball was snapped.</summary>
    public const string HuddleApproved = "huddle.approved";

    /// <summary>A workspace mode was selected (prop: mode — FreshFolder|PrWorktree|CurrentBranch).</summary>
    public const string WorkspaceModeSelected = "workspace_mode.selected";

    /// <summary>Parallelism level was set (prop: value).</summary>
    public const string ParallelismSet = "parallelism.set";

    /// <summary>Field position (source type) was selected (prop: source_type).</summary>
    public const string FieldPositionSet = "field_position.set";

    // ── Outcome ──────────────────────────────────────────────────────────────
    /// <summary>A Drive was started (prop: team_id, parallelism, workspace_mode).</summary>
    public const string DriveStarted = "drive.started";

    /// <summary>A Drive completed successfully — Touchdown!</summary>
    public const string DriveTouchdown = "drive.touchdown";

    /// <summary>A Drive failed — Turnover.</summary>
    public const string DriveTurnover = "drive.turnover";

    // ── Error ────────────────────────────────────────────────────────────────
    /// <summary>An unhandled exception was caught (prop: exception_type — never the message).</summary>
    public const string ErrorUnhandled = "error.unhandled";

    /// <summary>The agent process failed to start or crashed (prop: provider_id).</summary>
    public const string ErrorAgentProcess = "error.agent_process";

    /// <summary>A git worktree operation failed (prop: operation).</summary>
    public const string ErrorWorktree = "error.worktree";

    // ── Perf ─────────────────────────────────────────────────────────────────
    /// <summary>Total Drive duration in milliseconds.</summary>
    public const string PerfDriveDuration = "perf.drive_duration";

    /// <summary>Time to first token from the agent (ms).</summary>
    public const string PerfAgentFirstToken = "perf.agent_first_token";

    /// <summary>Duration of a single Huddle turn (ms).</summary>
    public const string PerfHuddleTurn = "perf.huddle_turn";

    /// <summary>Time to set up a git worktree (ms).</summary>
    public const string PerfWorktreeSetup = "perf.worktree_setup";
}

