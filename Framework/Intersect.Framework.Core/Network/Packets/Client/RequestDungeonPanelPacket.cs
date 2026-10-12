using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestDungeonPanelPacket : IntersectPacket
{
    public RequestDungeonPanelPacket()
    {
    }

    public RequestDungeonPanelPacket(bool openWindow, Guid playerId = default)
    {
        OpenWindow = openWindow;
        PlayerId = playerId;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }

    // Empty keeps the existing personal dungeon panel behavior.
    [Key(1)]
    public Guid PlayerId { get; set; }
}
