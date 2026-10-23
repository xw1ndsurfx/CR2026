using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class CampfireRequestPacket : IntersectPacket
{
    // Empty: open the inventory craft list. Otherwise: interact with an existing fire.
    [Key(0)] public Guid FireId { get; set; }
}
