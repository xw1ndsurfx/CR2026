using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Newtonsoft.Json;

namespace Intersect.Server.CraftingOrders;

internal sealed class CraftingOrderRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public Guid CrafterId { get; set; }
    public string Crafter { get; set; } = "";
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public int Reward { get; set; }
    public byte Status { get; set; }
    public DateTimeOffset Deadline { get; set; }
}

internal sealed class CraftingOrderStore
{
    public List<CraftingOrderRecord> Orders { get; set; } = [];
    public Dictionary<Guid, long> PendingAureons { get; set; } = [];
    public Dictionary<Guid, Dictionary<Guid, int>> PendingItems { get; set; } = [];
}

/// <summary>
/// Server-authoritative commissions. Payment is escrowed when posted; delivery
/// consumes the crafter's actual inventory and queues the item for the customer.
/// This is intentionally independent from the normal marketplace.
/// </summary>
internal static class CraftingOrdersRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "crafting-orders-state.json");
    private static CraftingOrderStore? _store;
    private static CraftingOrderStore Store => _store ??= Load();

    private static CraftingOrderStore Load()
    {
        try
        {
            if (File.Exists(PathName))
            {
                var state = JsonConvert.DeserializeObject<CraftingOrderStore>(File.ReadAllText(PathName));
                if (state != null)
                {
                    state.Orders ??= [];
                    state.PendingAureons ??= [];
                    state.PendingItems ??= [];
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            // Never discard a corrupted escrow file, which could destroy player balances.
            throw new InvalidOperationException("Cannot load crafting-orders escrow state.", ex);
        }
        return new CraftingOrderStore();
    }

    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(Store, Formatting.Indented));
        File.Move(temp, PathName, overwrite: true);
    }

    private static ItemDescriptor? Currency() =>
        ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().FirstOrDefault(item =>
            item.ItemType == ItemType.Currency &&
            (item.Name.Equals("Aureons", StringComparison.OrdinalIgnoreCase) ||
             item.Name.Equals("Aurons", StringComparison.OrdinalIgnoreCase) ||
             item.Name.Equals("Aureon", StringComparison.OrdinalIgnoreCase) ||
             item.Name.Equals("Auron", StringComparison.OrdinalIgnoreCase)));

    // Commissions are player-to-player deliveries, not Marketplace listings.
    // Marketplace selling is opt-in and defaults to false for most crafted items;
    // eligibility must instead respect the item's actual trading permission.
    private static bool Craftable(Guid itemId) =>
        ItemDescriptor.Get(itemId) is { CanTrade: true } item &&
        item.ItemType != ItemType.Bag &&
        item.ItemType != ItemType.Currency &&
        CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>()
            .Any(recipe => recipe.ItemId == itemId && recipe.Quantity > 0);

    private static void QueueAureons(Guid playerId, int amount)
    {
        Store.PendingAureons.TryGetValue(playerId, out var previous);
        Store.PendingAureons[playerId] = checked(previous + amount);
    }

    private static void QueueItem(Guid playerId, Guid itemId, int quantity)
    {
        if (!Store.PendingItems.TryGetValue(playerId, out var items))
            Store.PendingItems[playerId] = items = [];
        items.TryGetValue(itemId, out var previous);
        items[itemId] = checked(previous + quantity);
    }

    private static void Expire()
    {
        var expired = Store.Orders.Where(order => order.Status < 2 && order.Deadline <= DateTimeOffset.UtcNow).ToArray();
        if (expired.Length == 0) return;
        foreach (var order in expired)
        {
            QueueAureons(order.CustomerId, order.Reward);
            order.Status = 4; // expired/refunded
        }
        Save();
    }

    private static void DeliverPending(Player player)
    {
        var currency = Currency();
        if (currency == null) return;
        var changed = false;
        if (Store.PendingAureons.TryGetValue(player.Id, out var balance))
        {
            while (balance > 0)
            {
                var chunk = (int)Math.Min(balance, int.MaxValue);
                if (!player.TryGiveItem(currency.Id, chunk, ItemHandling.Normal, bankOverflow: true)) break;
                balance -= chunk;
                changed = true;
            }
            if (balance == 0) Store.PendingAureons.Remove(player.Id);
            else Store.PendingAureons[player.Id] = balance;
        }
        if (Store.PendingItems.TryGetValue(player.Id, out var items))
        {
            foreach (var (itemId, amount) in items.ToArray())
            {
                if (ItemDescriptor.Get(itemId) == null) continue;
                if (player.TryGiveItem(itemId, amount, ItemHandling.Normal, bankOverflow: true))
                {
                    items.Remove(itemId);
                    changed = true;
                }
            }
            if (items.Count == 0) Store.PendingItems.Remove(player.Id);
        }
        if (changed) { Save(); player.User?.Save(); }
    }

    private static CraftingOrdersStatePacket View(Player player, string message = "", bool open = false)
    {
        Expire();
        DeliverPending(player);
        var currency = Currency();
        return new CraftingOrdersStatePacket
        {
            OpenWindow = open,
            MessageKey = currency == null ? "MissingCurrency" : message,
            Aureons = currency == null ? 0 : player.FindInventoryItemQuantity(currency.Id),
            CraftableItemIds = CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>()
                .Where(recipe => recipe.ItemId != Guid.Empty && recipe.Quantity > 0)
                .Select(recipe => recipe.ItemId)
                .Distinct()
                .Where(Craftable)
                .OrderBy(ItemDescriptor.GetName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Orders = Store.Orders.Where(order => order.Status < 2 || order.CustomerId == player.Id || order.CrafterId == player.Id)
                .OrderBy(order => order.Status).ThenBy(order => order.Deadline)
                .Take(250).Select(order => new CraftingOrderView
                {
                    Id = order.Id, ItemId = order.ItemId, Quantity = order.Quantity,
                    Reward = order.Reward, Customer = order.Customer, Crafter = order.Crafter,
                    Status = order.Status, IsCustomer = order.CustomerId == player.Id,
                    IsCrafter = order.CrafterId == player.Id,
                    DeadlineUtcMs = order.Deadline.ToUnixTimeMilliseconds(),
                }).ToArray(),
        };
    }

    internal static CraftingOrdersStatePacket StateFor(Player player, bool open = false)
    {
        lock (Gate) return View(player, open: open);
    }

    internal static CraftingOrdersStatePacket Post(Player player, Guid itemId, int quantity, int reward, int hours)
    {
        lock (Gate)
        {
            if (!Craftable(itemId)) return View(player, "InvalidRecipe");
            if (quantity < 1 || quantity > 100 || reward < 1 || reward > 100000000 || hours < 1 || hours > 720)
                return View(player, "InvalidTerms");
            var currency = Currency();
            if (currency == null) return View(player, "MissingCurrency");
            if (player.FindInventoryItemQuantity(currency.Id) < reward || !player.TryTakeItem(currency.Id, reward))
                return View(player, "InsufficientFunds");

            var order = new CraftingOrderRecord
            {
                CustomerId = player.Id, Customer = player.Name, ItemId = itemId,
                Quantity = quantity, Reward = reward,
                Deadline = DateTimeOffset.UtcNow.AddHours(hours),
            };
            Store.Orders.Add(order);
            try { Save(); }
            catch
            {
                Store.Orders.Remove(order);
                player.TryGiveItem(currency.Id, reward, ItemHandling.Normal, bankOverflow: true);
                throw;
            }
            player.User?.Save();
            return View(player, "Posted");
        }
    }

    internal static CraftingOrdersStatePacket Act(Player player, Guid orderId, byte action)
    {
        lock (Gate)
        {
            Expire();
            var order = Store.Orders.FirstOrDefault(item => item.Id == orderId);
            if (order == null) return View(player, "Unavailable");
            switch (action)
            {
                case 0: // accept
                    if (order.Status != 0 || order.CustomerId == player.Id) return View(player, "Unavailable");
                    if (!Craftable(order.ItemId)) return View(player, "InvalidRecipe");
                    order.Status = 1;
                    order.CrafterId = player.Id;
                    order.Crafter = player.Name;
                    Save();
                    return View(player, "Accepted");
                case 1: // turn in an already crafted item from inventory
                    if (order.Status != 1 || order.CrafterId != player.Id) return View(player, "Unavailable");
                    if (player.FindInventoryItemQuantity(order.ItemId) < order.Quantity ||
                        !player.TryTakeItem(order.ItemId, order.Quantity)) return View(player, "MissingItems");
                    QueueItem(order.CustomerId, order.ItemId, order.Quantity);
                    QueueAureons(order.CrafterId, order.Reward);
                    order.Status = 2;
                    Save();
                    player.User?.Save();
                    return View(player, "Delivered");
                case 2: // only unaccepted orders can be withdrawn
                    if (order.Status != 0 || order.CustomerId != player.Id) return View(player, "Unavailable");
                    order.Status = 3;
                    QueueAureons(order.CustomerId, order.Reward);
                    Save();
                    return View(player, "Cancelled");
                default:
                    return View(player, "Unavailable");
            }
        }
    }
}
