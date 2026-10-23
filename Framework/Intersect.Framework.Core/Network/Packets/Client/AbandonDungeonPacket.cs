using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class AbandonDungeonPacket : IntersectPacket
{
    [Key(0)] public Guid RunId { get; set; }
}
