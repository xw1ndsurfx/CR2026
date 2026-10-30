using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class CampfiresPacket : IntersectPacket
{
    [Key(0)] public CampfireState[] Fires { get; set; } = [];
}
[MessagePackObject]
public sealed class CampfireState
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public int X { get; set; }
    [Key(3)] public int Y { get; set; }
    [Key(4)] public Guid AnimationId { get; set; }
    [Key(5)] public bool Burning { get; set; }
    [Key(6)] public int Z { get; set; }
}
