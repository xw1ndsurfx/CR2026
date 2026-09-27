using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Newtonsoft.Json;
using ServerItem = Intersect.Server.Database.Item;

namespace Intersect.Server.Marketplace;

internal sealed class MarketplacePersistedState
{
    public List<MarketplaceListingRecord> Listings { get; set; } = [];
    public Dictionary<Guid, long> PendingAurons { get; set; } = [];
    public Dictionary<Guid, List<MarketplacePendingItem>> PendingItems { get; set; } = [];
}

internal sealed class MarketplaceListingRecord
{
    public Guid ListingId { get; set; } = Guid.NewGuid();
    public Guid SellerPlayerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public ItemProperties Properties { get; set; } = new();
    public bool IsAuction { get; set; }
    public int AskingPrice { get; set; }
    public int CurrentBid { get; set; }
    public Guid CurrentBidderPlayerId { get; set; }
    public string CurrentBidderName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

internal sealed class MarketplacePendingItem
{
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public ItemProperties Properties { get; set; } = new();
}

internal static class MarketplaceRuntime
{
    private const int MaxDurationMinutes = 60 * 24 * 30;
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "marketplace-state.json");
    private static MarketplacePersistedState? _state;

    private static MarketplacePersistedState State => _state ??= LoadCore();

    internal static MarketplaceStatePacket BuildState(Player player, string message = "", bool success = false)
    {
        lock (Gate)
        {
            return BuildStateLocked(player, message, success);
        }
    }

    internal static MarketplaceStatePacket CreateListing(
        Player player,
        MarketplaceItemSource source,
        int slotIndex,
        int quantity,
        int price,
        bool isAuction,
        int durationMinutes
    )
    {
        lock (Gate)
        {
            ProcessExpiredLocked();

            if (price < 1)
            {
                return BuildStateLocked(player, "Price / starting bid must be at least 1 Auron.", false);
            }

            if (durationMinutes < 0 || durationMinutes > MaxDurationMinutes)
            {
                return BuildStateLocked(player, "Duration must be 0 (no expiry) or at most 30 days.", false);
            }

            var currency = ResolveAurons();
            if (currency == null)
            {
                return BuildStateLocked(player, "Marketplace currency not found. Create an ItemType.Currency named Aurons/Aureons.", false);
            }

            ServerItem? sourceItem;
            if (source == MarketplaceItemSource.Inventory)
            {
                if (slotIndex < 0 || slotIndex >= player.Items.Count)
                {
                    return BuildStateLocked(player, "Invalid inventory slot.", false);
                }

                sourceItem = player.Items[slotIndex];
            }
            else
            {
                if (slotIndex < 0 || slotIndex >= player.Bank.Count)
                {
                    return BuildStateLocked(player, "Invalid bank slot.", false);
                }

                sourceItem = player.Bank[slotIndex];
            }

            if (sourceItem == null || sourceItem.ItemId == Guid.Empty || sourceItem.Quantity < 1)
            {
                return BuildStateLocked(player, "That item is no longer available.", false);
            }

            var descriptor = sourceItem.Descriptor;
            if (descriptor == null || !descriptor.CanSellInMarketplace)
            {
                return BuildStateLocked(player, "This item cannot be sold in the Marketplace.", false);
            }

            if (sourceItem.ItemId == currency.Id)
            {
                return BuildStateLocked(player, "Aurons cannot be listed as a Marketplace item.", false);
            }

            if (descriptor.ItemType == ItemType.Bag || sourceItem.BagId != null)
            {
                return BuildStateLocked(player, "Bags cannot be listed in the Marketplace.", false);
            }

            if (quantity < 1 || quantity > sourceItem.Quantity)
            {
                return BuildStateLocked(player, "Invalid quantity.", false);
            }

            if (!descriptor.IsStackable && quantity != 1)
            {
                return BuildStateLocked(player, "Non-stackable items must be listed one at a time.", false);
            }

            var backup = new ServerItem(sourceItem);
            var escrowItem = new ServerItem(sourceItem) { Quantity = quantity };

            var removed = source == MarketplaceItemSource.Inventory
                ? player.TryTakeItem(player.Items[slotIndex], quantity)
                : TakeFromBank(player, slotIndex, quantity);

            if (!removed)
            {
                return BuildStateLocked(player, "The item could not be moved into Marketplace escrow.", false);
            }

            var listing = new MarketplaceListingRecord
            {
                ListingId = Guid.NewGuid(),
                SellerPlayerId = player.Id,
                SellerName = player.Name,
                ItemId = escrowItem.ItemId,
                Quantity = escrowItem.Quantity,
                Properties = new ItemProperties(escrowItem.Properties),
                IsAuction = isAuction,
                AskingPrice = price,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = durationMinutes == 0 ? null : DateTimeOffset.UtcNow.AddMinutes(durationMinutes),
            };

            State.Listings.Add(listing);

            try
            {
                SaveCore();
            }
            catch
            {
                State.Listings.Remove(listing);
                RestoreSource(player, source, slotIndex, backup);
                throw;
            }

            player.User?.Save();
            return BuildStateLocked(
                player,
                isAuction ? "Auction created. The item is now held in escrow." : "Marketplace listing created.",
                true
            );
        }
    }

    internal static MarketplaceStatePacket Buy(Player player, Guid listingId)
    {
        lock (Gate)
        {
            ProcessExpiredLocked();
            var listing = State.Listings.FirstOrDefault(entry => entry.ListingId == listingId);
            if (listing == null)
            {
                return BuildStateLocked(player, "This listing is no longer available.", false);
            }

            if (listing.IsAuction)
            {
                return BuildStateLocked(player, "This listing is an auction. Place a bid instead.", false);
            }

            if (listing.SellerPlayerId == player.Id)
            {
                return BuildStateLocked(player, "You cannot buy your own listing.", false);
            }

            var currency = ResolveAurons();
            if (currency == null)
            {
                return BuildStateLocked(player, "Marketplace currency not found.", false);
            }

            if (!TakeAurons(player, currency.Id, listing.AskingPrice))
            {
                return BuildStateLocked(player, "You do not have enough Aurons in your inventory.", false);
            }

            AddPendingAurons(listing.SellerPlayerId, listing.AskingPrice);
            AddPendingItem(player.Id, listing);
            State.Listings.Remove(listing);

            try
            {
                SaveCore();
            }
            catch
            {
                State.Listings.Add(listing);
                RemovePendingAurons(listing.SellerPlayerId, listing.AskingPrice);
                RemovePendingItem(player.Id, listing);
                player.TryGiveItem(currency.Id, listing.AskingPrice, ItemHandling.Normal, bankOverflow: true);
                player.User?.Save();
                throw;
            }

            player.User?.Save();
            DeliverPendingLocked(player, currency);
            SaveCore();

            return BuildStateLocked(player, "Purchase completed. The item was delivered to your inventory/bank.", true);
        }
    }

    internal static MarketplaceStatePacket Bid(Player player, Guid listingId, int amount)
    {
        lock (Gate)
        {
            ProcessExpiredLocked();
            var listing = State.Listings.FirstOrDefault(entry => entry.ListingId == listingId);
            if (listing == null)
            {
                return BuildStateLocked(player, "This auction is no longer available.", false);
            }

            if (!listing.IsAuction)
            {
                return BuildStateLocked(player, "This listing is not an auction.", false);
            }

            if (listing.SellerPlayerId == player.Id)
            {
                return BuildStateLocked(player, "You cannot bid on your own auction.", false);
            }

            var minimum = listing.CurrentBid > 0
                ? (listing.CurrentBid == int.MaxValue ? int.MaxValue : listing.CurrentBid + 1)
                : listing.AskingPrice;

            if (amount < minimum)
            {
                return BuildStateLocked(player, $"Minimum bid is {minimum:N0} Aurons.", false);
            }

            var currency = ResolveAurons();
            if (currency == null)
            {
                return BuildStateLocked(player, "Marketplace currency not found.", false);
            }

            var oldBid = listing.CurrentBid;
            var oldBidderId = listing.CurrentBidderPlayerId;
            var oldBidderName = listing.CurrentBidderName;
            var sameBidder = oldBidderId == player.Id;
            var amountToEscrow = sameBidder ? amount - oldBid : amount;

            if (amountToEscrow < 1 || !TakeAurons(player, currency.Id, amountToEscrow))
            {
                return BuildStateLocked(player, "You do not have enough Aurons in your inventory for this bid.", false);
            }

            if (!sameBidder && oldBidderId != Guid.Empty && oldBid > 0)
            {
                AddPendingAurons(oldBidderId, oldBid);
            }

            listing.CurrentBid = amount;
            listing.CurrentBidderPlayerId = player.Id;
            listing.CurrentBidderName = player.Name;

            try
            {
                SaveCore();
            }
            catch
            {
                listing.CurrentBid = oldBid;
                listing.CurrentBidderPlayerId = oldBidderId;
                listing.CurrentBidderName = oldBidderName;
                if (!sameBidder && oldBidderId != Guid.Empty && oldBid > 0)
                {
                    RemovePendingAurons(oldBidderId, oldBid);
                }

                player.TryGiveItem(currency.Id, amountToEscrow, ItemHandling.Normal, bankOverflow: true);
                player.User?.Save();
                throw;
            }

            player.User?.Save();
            return BuildStateLocked(player, $"Bid placed: {amount:N0} Aurons.", true);
        }
    }

    internal static MarketplaceStatePacket Cancel(Player player, Guid listingId)
    {
        lock (Gate)
        {
            ProcessExpiredLocked();
            var listing = State.Listings.FirstOrDefault(entry => entry.ListingId == listingId);
            if (listing == null)
            {
                return BuildStateLocked(player, "This listing is no longer available.", false);
            }

            if (listing.SellerPlayerId != player.Id)
            {
                return BuildStateLocked(player, "Only the seller can cancel this listing.", false);
            }

            if (listing.IsAuction && listing.CurrentBid > 0)
            {
                return BuildStateLocked(player, "An auction with an active bid cannot be cancelled. Accept the bid or let it expire.", false);
            }

            AddPendingItem(player.Id, listing);
            State.Listings.Remove(listing);
            SaveCore();

            var currency = ResolveAurons();
            if (currency != null)
            {
                DeliverPendingLocked(player, currency);
                SaveCore();
            }

            return BuildStateLocked(player, "Listing cancelled. The item was returned.", true);
        }
    }

    internal static MarketplaceStatePacket CompleteAuction(Player player, Guid listingId)
    {
        lock (Gate)
        {
            ProcessExpiredLocked();
            var listing = State.Listings.FirstOrDefault(entry => entry.ListingId == listingId);
            if (listing == null)
            {
                return BuildStateLocked(player, "This auction is no longer available.", false);
            }

            if (!listing.IsAuction || listing.SellerPlayerId != player.Id)
            {
                return BuildStateLocked(player, "Only the seller can complete this auction.", false);
            }

            if (listing.CurrentBidderPlayerId == Guid.Empty || listing.CurrentBid < 1)
            {
                return BuildStateLocked(player, "This auction does not have a bid yet.", false);
            }

            AddPendingItem(listing.CurrentBidderPlayerId, listing);
            AddPendingAurons(listing.SellerPlayerId, listing.CurrentBid);
            State.Listings.Remove(listing);
            SaveCore();

            var currency = ResolveAurons();
            if (currency != null)
            {
                DeliverPendingLocked(player, currency);
                SaveCore();
            }

            return BuildStateLocked(player, $"Auction completed for {listing.CurrentBid:N0} Aurons.", true);
        }
    }

    private static MarketplaceStatePacket BuildStateLocked(Player player, string message, bool success)
    {
        var expired = ProcessExpiredLocked();
        var currency = ResolveAurons();

        if (currency == null)
        {
            if (expired)
            {
                SaveCore();
            }

            return new MarketplaceStatePacket
            {
                OperationSucceeded = false,
                Message = "Marketplace currency not found. Create an ItemType.Currency named Aurons/Aureons.",
                Listings = BuildListings(player),
                SellableItems = [],
            };
        }

        var delivered = DeliverPendingLocked(player, currency);
        if (expired || delivered)
        {
            SaveCore();
        }

        State.PendingAurons.TryGetValue(player.Id, out var pending);

        return new MarketplaceStatePacket
        {
            CurrencyItemId = currency.Id,
            CurrencyName = currency.Name,
            CurrencyBalance = player.FindInventoryItemQuantity(currency.Id),
            PendingCurrency = pending,
            Listings = BuildListings(player),
            SellableItems = BuildSellableItems(player, currency.Id),
            OperationSucceeded = success,
            Message = message ?? string.Empty,
        };
    }

    private static MarketplaceListingState[] BuildListings(Player player) =>
        State.Listings
            .OrderBy(entry => entry.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => new MarketplaceListingState
            {
                ListingId = entry.ListingId,
                SellerPlayerId = entry.SellerPlayerId,
                SellerName = entry.SellerName,
                ItemId = entry.ItemId,
                Quantity = entry.Quantity,
                Properties = new ItemProperties(entry.Properties),
                IsAuction = entry.IsAuction,
                AskingPrice = entry.AskingPrice,
                CurrentBid = entry.CurrentBid,
                CurrentBidderName = entry.CurrentBidderName,
                ExpiresAtUnixMilliseconds = entry.ExpiresAtUtc?.ToUnixTimeMilliseconds() ?? 0,
                OwnListing = entry.SellerPlayerId == player.Id,
                CanCancel = entry.SellerPlayerId == player.Id && (!entry.IsAuction || entry.CurrentBid == 0),
                CanAcceptBid = entry.SellerPlayerId == player.Id && entry.IsAuction && entry.CurrentBid > 0,
            })
            .ToArray();

    private static MarketplaceOwnedItemState[] BuildSellableItems(Player player, Guid currencyId)
    {
        var result = new List<MarketplaceOwnedItemState>();

        for (var i = 0; i < player.Items.Count; ++i)
        {
            AddOwnedItem(result, MarketplaceItemSource.Inventory, i, player.Items[i], currencyId);
        }

        for (var i = 0; i < player.Bank.Count; ++i)
        {
            AddOwnedItem(result, MarketplaceItemSource.Bank, i, player.Bank[i], currencyId);
        }

        return result
            .OrderBy(entry => ItemDescriptor.GetName(entry.ItemId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Source)
            .ThenBy(entry => entry.Slot)
            .ToArray();
    }

    private static void AddOwnedItem(
        ICollection<MarketplaceOwnedItemState> result,
        MarketplaceItemSource source,
        int slot,
        ServerItem? item,
        Guid currencyId
    )
    {
        if (item == null || item.ItemId == Guid.Empty || item.Quantity < 1 || item.ItemId == currencyId)
        {
            return;
        }

        var descriptor = item.Descriptor;
        if (descriptor == null ||
            !descriptor.CanSellInMarketplace ||
            descriptor.ItemType == ItemType.Bag ||
            item.BagId != null)
        {
            return;
        }

        result.Add(new MarketplaceOwnedItemState
        {
            Source = source,
            Slot = slot,
            ItemId = item.ItemId,
            Quantity = item.Quantity,
            Properties = new ItemProperties(item.Properties),
        });
    }

    private static bool ProcessExpiredLocked()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = State.Listings
            .Where(entry => entry.ExpiresAtUtc is { } expires && expires <= now)
            .ToArray();

        if (expired.Length == 0)
        {
            return false;
        }

        foreach (var listing in expired)
        {
            if (listing.IsAuction && listing.CurrentBidderPlayerId != Guid.Empty && listing.CurrentBid > 0)
            {
                AddPendingItem(listing.CurrentBidderPlayerId, listing);
                AddPendingAurons(listing.SellerPlayerId, listing.CurrentBid);
            }
            else
            {
                AddPendingItem(listing.SellerPlayerId, listing);
            }

            State.Listings.Remove(listing);
        }

        return true;
    }

    private static bool DeliverPendingLocked(Player player, ItemDescriptor currency)
    {
        var changed = false;
        var playerChanged = false;

        if (State.PendingAurons.TryGetValue(player.Id, out var pendingAurons) && pendingAurons > 0)
        {
            while (pendingAurons > 0)
            {
                var chunk = (int)Math.Min(int.MaxValue, pendingAurons);
                if (!player.TryGiveItem(currency.Id, chunk, ItemHandling.Normal, bankOverflow: true))
                {
                    break;
                }

                pendingAurons -= chunk;
                changed = true;
                playerChanged = true;
            }

            if (pendingAurons <= 0)
            {
                State.PendingAurons.Remove(player.Id);
            }
            else
            {
                State.PendingAurons[player.Id] = pendingAurons;
            }
        }

        if (State.PendingItems.TryGetValue(player.Id, out var pendingItems))
        {
            for (var index = pendingItems.Count - 1; index >= 0; --index)
            {
                var pending = pendingItems[index];
                if (ItemDescriptor.Get(pending.ItemId) == null)
                {
                    continue;
                }

                var item = new ServerItem(
                    pending.ItemId,
                    pending.Quantity,
                    new ItemProperties(pending.Properties)
                );

                if (!player.TryGiveItem(item, ItemHandling.Normal, bankOverflow: true))
                {
                    continue;
                }

                pendingItems.RemoveAt(index);
                changed = true;
                playerChanged = true;
            }

            if (pendingItems.Count == 0)
            {
                State.PendingItems.Remove(player.Id);
            }
        }

        if (playerChanged)
        {
            player.User?.Save();
        }

        return changed;
    }

    private static bool TakeAurons(Player player, Guid currencyId, int amount) =>
        amount > 0 &&
        player.FindInventoryItemQuantity(currencyId) >= amount &&
        player.TryTakeItem(currencyId, amount);

    private static bool TakeFromBank(Player player, int slotIndex, int quantity)
    {
        var slot = player.Bank[slotIndex];
        if (slot == null || slot.ItemId == Guid.Empty || quantity < 1 || slot.Quantity < quantity)
        {
            return false;
        }

        slot.Quantity -= quantity;
        if (slot.Quantity < 1)
        {
            slot.Set(ServerItem.None);
        }

        if (player.InBank && !player.GuildBank)
        {
            player.BankInterface?.SendBankUpdate(slotIndex);
        }

        return true;
    }

    private static void RestoreSource(
        Player player,
        MarketplaceItemSource source,
        int slotIndex,
        ServerItem backup
    )
    {
        if (source == MarketplaceItemSource.Inventory)
        {
            player.Items[slotIndex].Set(backup);
            PacketSender.SendInventoryItemUpdate(player, slotIndex);
        }
        else
        {
            player.Bank[slotIndex].Set(backup);
            if (player.InBank && !player.GuildBank)
            {
                player.BankInterface?.SendBankUpdate(slotIndex);
            }
        }

        player.User?.Save();
    }

    private static void AddPendingAurons(Guid playerId, int amount)
    {
        if (playerId == Guid.Empty || amount < 1)
        {
            return;
        }

        State.PendingAurons.TryGetValue(playerId, out var existing);
        State.PendingAurons[playerId] = checked(existing + amount);
    }

    private static void RemovePendingAurons(Guid playerId, int amount)
    {
        if (!State.PendingAurons.TryGetValue(playerId, out var existing))
        {
            return;
        }

        var remaining = existing - amount;
        if (remaining <= 0)
        {
            State.PendingAurons.Remove(playerId);
        }
        else
        {
            State.PendingAurons[playerId] = remaining;
        }
    }

    private static void AddPendingItem(Guid playerId, MarketplaceListingRecord listing)
    {
        if (playerId == Guid.Empty)
        {
            return;
        }

        if (!State.PendingItems.TryGetValue(playerId, out var pending))
        {
            pending = [];
            State.PendingItems[playerId] = pending;
        }

        pending.Add(new MarketplacePendingItem
        {
            ItemId = listing.ItemId,
            Quantity = listing.Quantity,
            Properties = new ItemProperties(listing.Properties),
        });
    }

    private static void RemovePendingItem(Guid playerId, MarketplaceListingRecord listing)
    {
        if (!State.PendingItems.TryGetValue(playerId, out var pending))
        {
            return;
        }

        var index = pending.FindIndex(item =>
            item.ItemId == listing.ItemId &&
            item.Quantity == listing.Quantity &&
            JsonConvert.SerializeObject(item.Properties) == JsonConvert.SerializeObject(listing.Properties)
        );

        if (index >= 0)
        {
            pending.RemoveAt(index);
        }

        if (pending.Count == 0)
        {
            State.PendingItems.Remove(playerId);
        }
    }

    private static ItemDescriptor? ResolveAurons()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Auron",
            "Aurons",
            "Aureon",
            "Aureons",
        };

        return ItemDescriptor.Lookup.Values
            .OfType<ItemDescriptor>()
            .FirstOrDefault(item => item.ItemType == ItemType.Currency && names.Contains(item.Name.Trim()));
    }

    private static MarketplacePersistedState LoadCore()
    {
        try
        {
            if (!File.Exists(PathName))
            {
                return new MarketplacePersistedState();
            }

            var state = JsonConvert.DeserializeObject<MarketplacePersistedState>(File.ReadAllText(PathName))
                        ?? new MarketplacePersistedState();
            state.Listings ??= [];
            state.PendingAurons ??= [];
            state.PendingItems ??= [];
            return state;
        }
        catch
        {
            return new MarketplacePersistedState();
        }
    }

    private static void SaveCore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(State, Formatting.Indented));
        File.Move(temp, PathName, overwrite: true);
    }
}
