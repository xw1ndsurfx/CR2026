using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestPlayerProfilePacket : IntersectPacket
{
    public RequestPlayerProfilePacket()
    {
    }

    public RequestPlayerProfilePacket(Guid playerId, string playerName, bool openWindow)
    {
        PlayerId = playerId;
        PlayerName = playerName ?? string.Empty;
        OpenWindow = openWindow;
    }

    [Key(0)]
    public Guid PlayerId { get; set; }

    [Key(1)]
    public string PlayerName { get; set; } = string.Empty;

    [Key(2)]
    public bool OpenWindow { get; set; } = true;
}
