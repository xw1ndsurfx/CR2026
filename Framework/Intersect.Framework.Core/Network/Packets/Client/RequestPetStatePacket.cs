using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestPetStatePacket : IntersectPacket
{
    public RequestPetStatePacket()
    {
    }

    public RequestPetStatePacket(bool openWindow)
    {
        OpenWindow = openWindow;
    }

    [Key(0)]
    public bool OpenWindow { get; set; }
}
