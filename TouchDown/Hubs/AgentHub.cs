using Microsoft.AspNetCore.SignalR;
using TouchDown.Models;

namespace TouchDown.Hubs;

public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinDrive(string driveId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, driveId);
        _logger.LogInformation("Client {ConnectionId} joined drive {DriveId}", Context.ConnectionId, driveId);
    }

    public async Task LeaveDrive(string driveId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, driveId);
    }

    public async Task BroadcastPlay(AgentMessage message)
    {
        _logger.LogInformation("[{DriveId}] {From} -> {To}: {Type}", message.DriveId, message.FromAgent, message.ToAgent, message.Type);
        await Clients.Group(message.DriveId).SendAsync("ReceivePlay", message);
    }

    public async Task SendLog(string driveId, string agentName, string message, string level = "Info")
    {
        await Clients.Group(driveId).SendAsync("ReceiveLog", new
        {
            Timestamp = DateTime.UtcNow,
            AgentName = agentName,
            Message = message,
            Level = level
        });
    }

    public async Task UpdateAgentStatus(string driveId, string agentName, string status, int? progressPercent = null)
    {
        await Clients.Group(driveId).SendAsync("AgentStatusUpdate", new
        {
            AgentName = agentName,
            Status = status,
            ProgressPercent = progressPercent
        });
    }

    public async Task DriveCompleted(string driveId, string status)
    {
        await Clients.Group(driveId).SendAsync("DriveCompleted", new
        {
            DriveId = driveId,
            Status = status,
            CompletedAt = DateTime.UtcNow
        });
    }
}
