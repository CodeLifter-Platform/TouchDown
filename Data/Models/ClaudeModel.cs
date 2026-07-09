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
        ClaudeModel.Opus => "claude-opus-4-8",
        ClaudeModel.Sonnet => "claude-sonnet-4-6",
        ClaudeModel.Haiku => "claude-haiku-4-5",
        _ => "claude-sonnet-4-6"
    };

    public static string ToDisplayName(this ClaudeModel model) => model switch
    {
        ClaudeModel.Opus => "Claude Opus 4.8",
        ClaudeModel.Sonnet => "Claude Sonnet 4.6",
        ClaudeModel.Haiku => "Claude Haiku 4.5",
        _ => model.ToString()
    };
}
