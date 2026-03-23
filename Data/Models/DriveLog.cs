using System.ComponentModel.DataAnnotations;

namespace TD.Models;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    AgentOutput,
    System
}

public class DriveLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public LogLevel Level { get; set; } = LogLevel.Info;

    [MaxLength(100)]
    public string? AgentName { get; set; }

    [Required, MaxLength(10000)]
    public string Message { get; set; } = string.Empty;

    public int DriveId { get; set; }
    public Drive? Drive { get; set; }
}
