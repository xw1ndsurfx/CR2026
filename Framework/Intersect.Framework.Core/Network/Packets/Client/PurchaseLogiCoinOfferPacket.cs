using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class PurchaseLogiCoinOfferPacket : IntersectPacket
{
    public PurchaseLogiCoinOfferPacket()
    {
    }

    public PurchaseLogiCoinOfferPacket(Guid offerId, Guid purchaseId)
    {
        OfferId = offerId;
        PurchaseId = purchaseId;
    }

    [Key(0)]
    public Guid OfferId { get; set; }

    [Key(1)]
    public Guid PurchaseId { get; set; }
}
