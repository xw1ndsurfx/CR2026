using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class ItemChangeNotificationPacket : IntersectPacket
{
    public ItemChangeNotificationPacket()
    {
    }

    public ItemChangeNotificationPacket(Guid itemId, int signedQuantity)
    {
        ItemId = itemId;
        SignedQuantity = signedQuantity;
    }

    [Key(0)]
    public Guid ItemId { get; set; }

    [Key(1)]
    public int SignedQuantity { get; set; }
}
