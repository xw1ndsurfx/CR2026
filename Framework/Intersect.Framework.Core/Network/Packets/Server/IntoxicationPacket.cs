using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class IntoxicationPacket : IntersectPacket
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public int GreenMilliseconds { get; set; }
    [Key(2)] public bool Vomit { get; set; }
}
