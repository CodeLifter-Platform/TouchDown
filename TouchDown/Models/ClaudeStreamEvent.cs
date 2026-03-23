using System.Text.Json.Serialization;

namespace TouchDown.Models;

/// <summary>
/// Represents events from claude --output-format stream-json.
/// Each line of stdout is a JSON object with a "type" field.
/// </summary>
public class ClaudeStreamEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // --- system events ---
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    // --- assistant message chunks ---
    [JsonPropertyName("content_block")]
    public ContentBlock? ContentBlock { get; set; }

    [JsonPropertyName("delta")]
    public DeltaBlock? Delta { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    // --- tool use events ---
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; set; }

    [JsonPropertyName("tool_input")]
    public object? ToolInput { get; set; }

    // --- result events ---
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("cost_usd")]
    public double? CostUsd { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("duration_api_ms")]
    public long? DurationApiMs { get; set; }

    [JsonPropertyName("num_turns")]
    public int? NumTurns { get; set; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }
}

public class ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class DeltaBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Structured plan the Quarterback produces as JSON.
/// </summary>
public class QuarterbackPlan
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("assignments")]
    public List<PlayAssignment> Assignments { get; set; } = [];
}

public class PlayAssignment
{
    [JsonPropertyName("agent_role")]
    public string AgentRole { get; set; } = string.Empty;

    [JsonPropertyName("agent_name")]
    public string? AgentName { get; set; }

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("depends_on")]
    public List<int>? DependsOn { get; set; }

    [JsonPropertyName("files")]
    public List<string>? Files { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}
