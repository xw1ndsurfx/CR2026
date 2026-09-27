using Intersect.Network.Packets;
using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestMarketplaceState() =>
        Network.SendPacket(new RequestMarketplaceStatePacket());

    public static void SendCreateMarketplaceListing(
        MarketplaceItemSource source,
        int slot,
        int quantity,
        int price,
        bool isAuction,
        int durationMinutes
    ) =>
        Network.SendPacket(
            new CreateMarketplaceListingPacket(source, slot, quantity, price, isAuction, durationMinutes)
        );

    public static void SendBuyMarketplaceListing(Guid listingId) =>
        Network.SendPacket(new BuyMarketplaceListingPacket(listingId));

    public static void SendMarketplaceBid(Guid listingId, int amount) =>
        Network.SendPacket(new BidMarketplaceListingPacket(listingId, amount));

    public static void SendCancelMarketplaceListing(Guid listingId) =>
        Network.SendPacket(new CancelMarketplaceListingPacket(listingId));

    public static void SendCompleteMarketplaceAuction(Guid listingId) =>
        Network.SendPacket(new CompleteMarketplaceAuctionPacket(listingId));
}
