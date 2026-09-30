using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestAchievementStatePacket : IntersectPacket
{
    public RequestAchievementStatePacket()
    {
    }

    public RequestAchievementStatePacket(bool openWindow)
    {
        OpenWindow = openWindow;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }
}
