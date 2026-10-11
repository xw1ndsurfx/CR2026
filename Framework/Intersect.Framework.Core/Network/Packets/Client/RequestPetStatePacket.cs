using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestPetStatePacket : IntersectPacket
{
    public RequestPetStatePacket()
    {
    }

    public RequestPetStatePacket(bool openWindow, Guid playerId = default)
    {
        OpenWindow = openWindow;
        PlayerId = playerId;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }

    [Key(1)]
    public Guid PlayerId { get; set; }
}
