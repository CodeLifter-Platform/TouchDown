namespace TD.Services.Telemetry;

/// <summary>
/// Telemetry event and attribute names. Callers use these instead of magic strings.
/// </summary>
public static class TelemetryEvent
{
    // ── Usage ────────────────────────────────────────────────────────────────
    /// <summary>A wizard step was completed (prop: step_index, step_name).</summary>
    public const string WizardStepCompleted = "wizard.step_completed";

    /// <summary>A preset team was selected (prop: team_id, team_name).</summary>
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
    /// <summary>A Drive was started (prop: team_id, parallelism, workspace_mode, provider, model).</summary>
    public const string DriveStarted = "drive.started";

    /// <summary>A Drive completed successfully — Touchdown!</summary>
    public const string DriveTouchdown = "drive.touchdown";

    /// <summary>A Drive failed — Turnover (prop: turnover_reason).</summary>
    public const string DriveTurnover = "drive.turnover";

    // ── Plan quality ─────────────────────────────────────────────────────────
    /// <summary>
    /// How the Quarterback's plan was obtained (prop: source — huddle_json|reprompt|fallback).
    /// The fallback plan ignores the huddle entirely but the drive still runs, so without
    /// this a collapse in plan quality is invisible.
    /// </summary>
    public const string PlanResolved = "plan.resolved";

    /// <summary>
    /// Shape of the execution schedule (prop: play_count, wave_count, max_wave_width).
    /// If waves are mostly one play wide, parallelism and fan-out limits are doing nothing.
    /// </summary>
    public const string PlanWaveShape = "plan.wave_shape";

    /// <summary>The dependency graph contained a cycle and was broken to avoid deadlock.</summary>
    public const string PlanDependencyCycle = "plan.dependency_cycle";

    /// <summary>
    /// Two plays running in the same wave declared overlapping files
    /// (prop: overlap_count, wave_index). Fan-out slices are supposed to be disjoint.
    /// </summary>
    public const string PlanFanOutOverlap = "plan.fan_out_overlap";

    /// <summary>An assignment named an agent that is not on the roster (prop: agent_role, agent_name).</summary>
    public const string PlanAssignmentUnmatched = "plan.assignment_unmatched";

    // ── Error ────────────────────────────────────────────────────────────────
    /// <summary>An unhandled exception was caught.</summary>
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

/// <summary>Span and event attribute keys, kept in one place so queries stay stable.</summary>
public static class TelemetryAttribute
{
    public const string DriveId = "td.drive.id";
    public const string DriveStatus = "td.drive.status";
    public const string TaskDescription = "td.drive.task";
    public const string RepoPath = "td.drive.repo_path";
    public const string Branch = "td.drive.branch";
    public const string WorkspaceMode = "td.drive.workspace_mode";
    public const string MaxParallelism = "td.drive.max_parallelism";
    public const string TeamName = "td.team.name";

    public const string ProviderId = "td.provider.id";
    public const string ModelId = "td.model.id";
    public const string Effort = "td.model.effort";

    public const string PlayId = "td.play.id";
    public const string PlayIndex = "td.play.index";
    public const string PlayStatus = "td.play.status";
    public const string PlayDescription = "td.play.description";
    public const string WaveIndex = "td.play.wave_index";

    public const string AgentName = "td.agent.name";
    public const string AgentRole = "td.agent.role";
    public const string AgentOutput = "td.agent.output";
    public const string ToolsUsed = "td.agent.tools_used";

    public const string CostUsd = "td.cost.usd";
    public const string DurationMs = "td.duration.ms";
    public const string NumTurns = "td.agent.turns";

    public const string PlanSource = "td.plan.source";
    public const string PlanSummary = "td.plan.summary";
    public const string PlayCount = "td.plan.play_count";
    public const string WaveCount = "td.plan.wave_count";
    public const string MaxWaveWidth = "td.plan.max_wave_width";

    public const string ErrorType = "td.error.type";
    public const string ErrorMessage = "td.error.message";
    public const string ErrorStack = "td.error.stack";
    public const string Component = "td.component";
}

/// <summary>How the Quarterback's plan was obtained, in decreasing order of quality.</summary>
public enum PlanSource
{
    /// <summary>Structured JSON was extracted straight from the huddle conversation.</summary>
    HuddleJson,

    /// <summary>The huddle had no usable plan, so the Quarterback was re-prompted for one.</summary>
    Reprompt,

    /// <summary>Both attempts failed; a mechanical plan was generated, ignoring the huddle.</summary>
    Fallback
}
