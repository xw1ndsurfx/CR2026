using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestAchievementStatePacket : IntersectPacket
{
    public RequestAchievementStatePacket()
    {
    }

    public RequestAchievementStatePacket(bool openWindow, Guid playerId = default)
    {
        OpenWindow = openWindow;
        PlayerId = playerId;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }

    [Key(1)]
    public Guid PlayerId { get; set; }
}
