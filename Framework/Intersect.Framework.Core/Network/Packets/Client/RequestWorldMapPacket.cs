using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class RequestWorldMapPacket : IntersectPacket
{
    [Key(0)] public bool StateOnly { get; set; }
    [Key(1)] public bool IncludeShips { get; set; } = true;
}
