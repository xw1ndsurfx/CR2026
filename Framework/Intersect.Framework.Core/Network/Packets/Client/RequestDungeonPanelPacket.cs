using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestDungeonPanelPacket : IntersectPacket
{
    public RequestDungeonPanelPacket()
    {
    }

    public RequestDungeonPanelPacket(bool openWindow)
    {
        OpenWindow = openWindow;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }
}
