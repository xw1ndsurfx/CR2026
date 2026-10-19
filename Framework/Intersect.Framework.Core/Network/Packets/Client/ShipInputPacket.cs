using MessagePack;
namespace Intersect.Network.Packets.Client;

[MessagePackObject(AllowPrivate = true)]
public partial class ShipInputPacket : IntersectPacket
{
    [Key(0)] public Guid ShipId { get; set; }
    [Key(1)] public sbyte Thrust { get; set; }
    [Key(2)] public sbyte Turn { get; set; }
    [Key(3)] public bool Anchor { get; set; }
}
