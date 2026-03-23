namespace TD.Models;

public enum ClaudeModel
{
    Opus,
    Sonnet,
    Haiku
}

public static class ClaudeModelExtensions
{
    public static string ToModelId(this ClaudeModel model) => model switch
    {
        ClaudeModel.Opus => "claude-opus-4-6",
        ClaudeModel.Sonnet => "claude-sonnet-4-6",
        ClaudeModel.Haiku => "claude-haiku-4-5-20251001",
        _ => "claude-sonnet-4-6"
    };

    public static string ToDisplayName(this ClaudeModel model) => model switch
    {
        ClaudeModel.Opus => "Claude Opus",
        ClaudeModel.Sonnet => "Claude Sonnet",
        ClaudeModel.Haiku => "Claude Haiku",
        _ => model.ToString()
    };
}
