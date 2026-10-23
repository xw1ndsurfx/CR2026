using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class DungeonChestPacket : IntersectPacket
{
    [Key(0)] public Guid RunId { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public int X { get; set; }
    [Key(3)] public int Y { get; set; }
    [Key(4)] public Guid AnimationId { get; set; }
    [Key(5)] public bool Visible { get; set; }
    [Key(6)] public bool Claimed { get; set; }
    [Key(7)] public int SecondsRemaining { get; set; }
}
