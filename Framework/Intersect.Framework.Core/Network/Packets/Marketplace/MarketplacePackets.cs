using Intersect.Framework.Core.GameObjects.Items;
using MessagePack;

namespace Intersect.Network.Packets
{
    public enum MarketplaceItemSource : byte
    {
        Inventory = 0,
        Bank = 1,
    }

    [MessagePackObject]
    public sealed class MarketplaceListingState
    {
        [Key(0)] public Guid ListingId { get; set; }
        [Key(1)] public Guid SellerPlayerId { get; set; }
        [Key(2)] public string SellerName { get; set; } = string.Empty;
        [Key(3)] public Guid ItemId { get; set; }
        [Key(4)] public int Quantity { get; set; }
        [Key(5)] public ItemProperties Properties { get; set; } = new();
        [Key(6)] public bool IsAuction { get; set; }
        [Key(7)] public int AskingPrice { get; set; }
        [Key(8)] public int CurrentBid { get; set; }
        [Key(9)] public string CurrentBidderName { get; set; } = string.Empty;
        [Key(10)] public long ExpiresAtUnixMilliseconds { get; set; }
        [Key(11)] public bool OwnListing { get; set; }
        [Key(12)] public bool CanCancel { get; set; }
        [Key(13)] public bool CanAcceptBid { get; set; }
    }

    [MessagePackObject]
    public sealed class MarketplaceOwnedItemState
    {
        [Key(0)] public MarketplaceItemSource Source { get; set; }
        [Key(1)] public int Slot { get; set; }
        [Key(2)] public Guid ItemId { get; set; }
        [Key(3)] public int Quantity { get; set; }
        [Key(4)] public ItemProperties Properties { get; set; } = new();
    }
}

namespace Intersect.Network.Packets.Client
{
    [MessagePackObject]
    public partial class RequestMarketplaceStatePacket : IntersectPacket
    {
    }

    [MessagePackObject]
    public partial class CreateMarketplaceListingPacket : IntersectPacket
    {
        public CreateMarketplaceListingPacket()
        {
        }

        public CreateMarketplaceListingPacket(
            MarketplaceItemSource source,
            int slot,
            int quantity,
            int price,
            bool isAuction,
            int durationMinutes
        )
        {
            Source = source;
            Slot = slot;
            Quantity = quantity;
            Price = price;
            IsAuction = isAuction;
            DurationMinutes = durationMinutes;
        }

        [Key(0)] public MarketplaceItemSource Source { get; set; }
        [Key(1)] public int Slot { get; set; }
        [Key(2)] public int Quantity { get; set; }
        [Key(3)] public int Price { get; set; }
        [Key(4)] public bool IsAuction { get; set; }
        [Key(5)] public int DurationMinutes { get; set; }
    }

    [MessagePackObject]
    public partial class BuyMarketplaceListingPacket : IntersectPacket
    {
        public BuyMarketplaceListingPacket()
        {
        }

        public BuyMarketplaceListingPacket(Guid listingId)
        {
            ListingId = listingId;
        }

        [Key(0)] public Guid ListingId { get; set; }
    }

    [MessagePackObject]
    public partial class BidMarketplaceListingPacket : IntersectPacket
    {
        public BidMarketplaceListingPacket()
        {
        }

        public BidMarketplaceListingPacket(Guid listingId, int amount)
        {
            ListingId = listingId;
            Amount = amount;
        }

        [Key(0)] public Guid ListingId { get; set; }
        [Key(1)] public int Amount { get; set; }
    }

    [MessagePackObject]
    public partial class CancelMarketplaceListingPacket : IntersectPacket
    {
        public CancelMarketplaceListingPacket()
        {
        }

        public CancelMarketplaceListingPacket(Guid listingId)
        {
            ListingId = listingId;
        }

        [Key(0)] public Guid ListingId { get; set; }
    }

    [MessagePackObject]
    public partial class CompleteMarketplaceAuctionPacket : IntersectPacket
    {
        public CompleteMarketplaceAuctionPacket()
        {
        }

        public CompleteMarketplaceAuctionPacket(Guid listingId)
        {
            ListingId = listingId;
        }

        [Key(0)] public Guid ListingId { get; set; }
    }
}

namespace Intersect.Network.Packets.Server
{
    [MessagePackObject]
    public partial class MarketplaceStatePacket : IntersectPacket
    {
        [Key(0)] public Guid CurrencyItemId { get; set; }
        [Key(1)] public string CurrencyName { get; set; } = "Aurons";
        [Key(2)] public int CurrencyBalance { get; set; }
        [Key(3)] public long PendingCurrency { get; set; }
        [Key(4)] public MarketplaceListingState[] Listings { get; set; } = [];
        [Key(5)] public MarketplaceOwnedItemState[] SellableItems { get; set; } = [];
        [Key(6)] public bool OperationSucceeded { get; set; }
        [Key(7)] public string Message { get; set; } = string.Empty;
    }
}
