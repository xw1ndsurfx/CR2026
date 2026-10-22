using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class DungeonChestRequestPacket : IntersectPacket
{
    [Key(0)] public Guid RunId { get; set; }
}
