using Intersect.Network.Packets.Client;
using Intersect.Server.Marketplace;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestMarketplaceStatePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(MarketplaceRuntime.BuildState(player));
        }
    }

    public void HandlePacket(Client client, CreateMarketplaceListingPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(
                MarketplaceRuntime.CreateListing(
                    player,
                    packet.Source,
                    packet.Slot,
                    packet.Quantity,
                    packet.Price,
                    packet.IsAuction,
                    packet.DurationMinutes
                )
            );
        }
    }

    public void HandlePacket(Client client, BuyMarketplaceListingPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(MarketplaceRuntime.Buy(player, packet.ListingId));
        }
    }

    public void HandlePacket(Client client, BidMarketplaceListingPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(MarketplaceRuntime.Bid(player, packet.ListingId, packet.Amount));
        }
    }

    public void HandlePacket(Client client, CancelMarketplaceListingPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(MarketplaceRuntime.Cancel(player, packet.ListingId));
        }
    }

    public void HandlePacket(Client client, CompleteMarketplaceAuctionPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
        {
            return;
        }

        lock (player.EntityLock)
        {
            client.Send(MarketplaceRuntime.CompleteAuction(player, packet.ListingId));
        }
    }
}
