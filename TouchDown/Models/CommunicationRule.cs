namespace TouchDown.Models;

public enum CommStyle
{
    Broadcast,
    LeaderGated,
    Sequential,
    PeerToPeer
}

public class CommunicationRule
{
    public int Id { get; set; }
    public CommStyle Style { get; set; } = CommStyle.LeaderGated;
    public string? Description { get; set; }
    public int AgentTeamId { get; set; }
    public AgentTeam? AgentTeam { get; set; }
}
