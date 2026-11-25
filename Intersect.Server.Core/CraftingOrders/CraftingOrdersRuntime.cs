using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Intersect.Server.CraftingOrders;

internal sealed class CraftingOrderRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
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
    // Old escrow is imported once; keep the original JSON file untouched.
    public Dictionary<Guid, Dictionary<Guid, int>> PendingItems { get; set; } = [];
    public Dictionary<Guid, List<Item>> Deliveries { get; set; } = [];
}

internal static class CraftingOrdersRuntime
{
    private static readonly CraftingOrdersService Service = new(() => DbInterface.CreatePlayerContext(false),
        Path.Combine("resources", "crafting-orders-state.json"));
    internal static CraftingOrdersStatePacket StateFor(Player player, bool open = false) => Service.Run(player, open: open);
    internal static CraftingOrdersStatePacket Post(Player player, Guid item, int quantity, int reward, int hours, Guid requestId = default) =>
        Service.Run(player, store => Service.Post(store, player, item, quantity, reward, hours, requestId));
    internal static CraftingOrdersStatePacket Act(Player player, Guid id, byte action) =>
        Service.Run(player, store => Service.Act(store, player, id, action));
}

// Escrow, actual item instances, inventory and bank use one player-DB transaction.
// No mutable global state cache survives a rollback or hides a failed save.
internal sealed class CraftingOrdersService(Func<PlayerContext> contextFactory, string legacyPath)
{
    private static readonly object Gate = new();
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;
    internal Action? BeforeCommit { get; set; }

    internal static ItemDescriptor? Currency() => ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().FirstOrDefault(item =>
        item.ItemType == ItemType.Currency && new[] { "Aureons", "Aurons", "Aureon", "Auron" }.Contains(item.Name, StringComparer.OrdinalIgnoreCase));
    internal static bool Craftable(Guid id) => ItemDescriptor.Get(id) is { CanTrade: true } item &&
        item.ItemType is not (ItemType.Bag or ItemType.Currency) &&
        CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>().Any(r => r.ItemId == id && r.Quantity > 0 && r.EquipmentUpgrade == null);

    internal CraftingOrdersStatePacket Run(Player player, Func<CraftingOrderStore, string>? action = null, bool open = false)
    {
        if (player.User == null) return new() { MessageKey = "SaveFailed", OpenWindow = open };
        lock (player.EntityLock)
        lock (player.User.PokerSaveGate)
        lock (Gate)
        {
            var inventory = player.Items.Select(i => i.Clone()).ToArray();
            var bank = player.Bank.Select(i => i.Clone()).ToArray();
            var committed = false;
            try
            {
                using var db = contextFactory();
                using var transaction = db.Database.BeginTransaction();
                var ledger = db.CraftingOrdersLedger.SingleOrDefault(e => e.Id == 1);
                var json = ledger?.State ?? (File.Exists(legacyPath) ? File.ReadAllText(legacyPath) : "{}");
                var store = JsonConvert.DeserializeObject<CraftingOrderStore>(json) ?? throw new InvalidDataException("Empty crafting escrow.");
                if (store.Orders == null || store.PendingAureons == null || store.PendingItems == null || store.Deliveries == null)
                    throw new InvalidDataException("Invalid crafting escrow collections.");
                if (store.Orders.Any(o => o == null || o.Id == Guid.Empty || o.CustomerId == Guid.Empty ||
                        o.ItemId == Guid.Empty || o.Quantity is < 1 or > 100 || o.Reward is < 1 or > 100000000 ||
                        o.Status > 4 || (o.Status is 1 or 2 && o.CrafterId == Guid.Empty)) ||
                    store.Orders.Select(o => o.Id).Distinct().Count() != store.Orders.Count ||
                    store.PendingAureons.Any(e => e.Key == Guid.Empty || e.Value < 0) ||
                    store.PendingItems.Any(e => e.Key == Guid.Empty || e.Value == null || e.Value.Any(i => i.Key == Guid.Empty || i.Value < 1)) ||
                    store.Deliveries.Any(e => e.Key == Guid.Empty || e.Value == null || e.Value.Any(i => i == null || i.ItemId == Guid.Empty || i.Quantity < 1)))
                    throw new InvalidDataException("Invalid crafting escrow values.");
                foreach (var (owner, entries) in store.PendingItems)
                    foreach (var (id, count) in entries)
                    {
                        if (count < 1) throw new InvalidDataException("Invalid legacy delivery.");
                        if (!store.Deliveries.TryGetValue(owner, out var list)) store.Deliveries[owner] = list = [];
                        list.Add(new Item(id, count));
                    }
                store.PendingItems.Clear();
                foreach (var order in store.Orders.Where(o => o.Status < 2 && o.Deadline <= Now()))
                { Credit(store, order.CustomerId, order.Reward); order.Status = 4; }

                var busy = player.IsDead || player.HasPendingDeath || player.Trading.Counterparty != null || player.InBank || player.OpenCraftingTableId != Guid.Empty;
                var message = busy ? "Busy" : action?.Invoke(store) ?? "";
                if (!busy) DeliverPending(store, player);
                var updated = JsonConvert.SerializeObject(store);
                if (ledger == null) { ledger = new(); db.CraftingOrdersLedger.Add(ledger); }
                var changed = ledger.State != updated || !Same(inventory, player.Items) || !Same(bank, player.Bank);
                if (changed)
                {
                    ledger.State = updated; ledger.Revision++;
                    if (player.User.Save(db, force: true) != UserSaveResult.Completed)
                        throw new IOException("Crafting account save failed.");
                    BeforeCommit?.Invoke();
                }
                transaction.Commit(); committed = true;
                var view = View(store, player, message, open);
                // Notification failures must not undo a committed payment.
                if (!Same(inventory, player.Items) && player.Client != null)
                {
                    try { player.NotifyCraftingOrderInventory(inventory); }
                    catch (Exception ex) { ApplicationContext.Context.Value?.Logger.LogError(ex, "Crafting inventory notification failed"); }
                }
                return view;
            }
            catch (Exception ex)
            {
                ApplicationContext.Context.Value?.Logger.LogError(ex, "Crafting orders transaction failed");
                return new() { MessageKey = "SaveFailed", OpenWindow = open };
            }
            finally
            {
                if (!committed)
                {
                    for (var i = 0; i < inventory.Length; i++) player.Items[i].Set(inventory[i]);
                    for (var i = 0; i < bank.Length; i++) player.Bank[i].Set(bank[i]);
                }
            }
        }
    }

    private static bool Same(IEnumerable<Item> a, IEnumerable<Item> b) => JsonConvert.SerializeObject(a.Select(i => i.Clone())) == JsonConvert.SerializeObject(b.Select(i => i.Clone()));
    private static void Credit(CraftingOrderStore store, Guid owner, int amount) =>
        store.PendingAureons[owner] = checked(store.PendingAureons.GetValueOrDefault(owner) + amount);

    internal string Post(CraftingOrderStore store, Player player, Guid item, int quantity, int reward, int hours, Guid requestId)
    {
        if (requestId != Guid.Empty && store.Orders.Any(o => o.CustomerId == player.Id && o.RequestId == requestId)) return "AlreadyPosted";
        if (!Craftable(item)) return "InvalidRecipe";
        if (quantity is < 1 or > 100 || reward is < 1 or > 100000000 || hours is < 1 or > 720) return "InvalidTerms";
        var currency = Currency(); if (currency == null) return "MissingCurrency";
        if (!Take(player, currency.Id, reward, out _)) return "InsufficientFunds";
        store.Orders.Add(new() { CustomerId = player.Id, Customer = player.Name, ItemId = item,
            Quantity = quantity, Reward = reward, Deadline = Now().AddHours(hours), RequestId = requestId });
        return "Posted";
    }

    internal string Act(CraftingOrderStore store, Player player, Guid id, byte action)
    {
        var order = store.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null) return "Unavailable";
        switch (action)
        {
            case 0:
                if (order.Status != 0 || order.CustomerId == player.Id) return "Unavailable";
                if (!Craftable(order.ItemId)) return "InvalidRecipe";
                order.Status = 1; order.CrafterId = player.Id; order.Crafter = player.Name; return "Accepted";
            case 1:
                if (order.Status != 1 || order.CrafterId != player.Id) return "Unavailable";
                if (!Craftable(order.ItemId)) return "InvalidRecipe";
                if (!Take(player, order.ItemId, order.Quantity, out var delivered)) return "MissingItems";
                if (!store.Deliveries.TryGetValue(order.CustomerId, out var list)) store.Deliveries[order.CustomerId] = list = [];
                list.AddRange(delivered); Credit(store, player.Id, order.Reward); order.Status = 2; return "Delivered";
            case 2:
                if (order.Status != 0 || order.CustomerId != player.Id) return "Unavailable";
                Credit(store, player.Id, order.Reward); order.Status = 3; return "Cancelled";
            default: return "Unavailable";
        }
    }

    private static bool Take(Player player, Guid id, int count, out List<Item> taken)
    {
        taken = [];
        var descriptor = ItemDescriptor.Get(id)!;
        var slots = player.Items.Where(s => s.ItemId == id && s.Quantity > 0 && !player.Equipment.Contains(s.Slot)).ToArray();
        if (slots.Sum(s => descriptor.IsStackable ? (long)s.Quantity : 1) < count) return false;
        foreach (var slot in slots)
        {
            var amount = Math.Min(count, descriptor.IsStackable ? slot.Quantity : 1);
            var item = slot.Clone(); item.Quantity = amount; taken.Add(item);
            slot.Quantity -= amount; if (slot.Quantity <= 0) slot.Set(Item.None);
            count -= amount; if (count == 0) break;
        }
        return true;
    }

    // Only the quantity actually placed is removed from escrow. Equipment retains
    // its rolled stats, rarity, spell and event enhancements, including across restart.
    private static int Give(IEnumerable<Item> slots, Item source, bool bank)
    {
        var descriptor = source.Descriptor;
        if (descriptor == null) return 0;
        var remaining = source.Quantity;
        var maximum = descriptor.IsStackable ? Math.Max(1, bank ? descriptor.MaxBankStack : descriptor.MaxInventoryStack) : 1;
        var properties = JsonConvert.SerializeObject(source.Properties);
        foreach (var slot in slots.OrderBy(s => s.ItemId == Guid.Empty ? 1 : 0))
        {
            if (slot.ItemId != Guid.Empty && (!descriptor.IsStackable || slot.ItemId != source.ItemId ||
                JsonConvert.SerializeObject(slot.Properties) != properties)) continue;
            var space = slot.ItemId == Guid.Empty ? maximum : Math.Max(0L, (long)maximum - slot.Quantity);
            var amount = (int)Math.Min(remaining, space); if (amount == 0) continue;
            if (slot.ItemId == Guid.Empty) { slot.Set(source); slot.Quantity = amount; } else slot.Quantity += amount;
            remaining -= amount; if (remaining == 0) break;
        }
        return source.Quantity - remaining;
    }

    private static int Give(Player player, Item source)
    {
        var copy = source.Clone(); var inventory = Give(player.Items, copy, false); copy.Quantity -= inventory;
        var bankIsOpen = player.BankAccount.Opened ||
            (player.BankAccount.BaseSlots == 0 && player.Bank.Any(i => i.ItemId != Guid.Empty));
        var bankLimit = bankIsOpen && source.Descriptor?.CanBank == true
            ? (player.HasBankPremium ? player.Bank.Capacity : player.BaseBankSlots) : 0;
        var accessibleBank = player.Bank.Take(bankLimit);
        return inventory + (copy.Quantity > 0 ? Give(accessibleBank, copy, true) : 0);
    }
    private static void DeliverPending(CraftingOrderStore store, Player player)
    {
        if (Currency() is { } currency && store.PendingAureons.TryGetValue(player.Id, out var balance))
        {
            while (balance > 0)
            {
                var amount = (int)Math.Min(balance, int.MaxValue); var given = Give(player, new Item(currency.Id, amount));
                balance -= given; if (given < amount) break;
            }
            if (balance == 0) store.PendingAureons.Remove(player.Id); else store.PendingAureons[player.Id] = balance;
        }
        if (store.Deliveries.TryGetValue(player.Id, out var pending))
        {
            foreach (var item in pending.ToArray())
            { item.Quantity -= Give(player, item); if (item.Quantity == 0) pending.Remove(item); }
            if (pending.Count == 0) store.Deliveries.Remove(player.Id);
        }
    }
    private static CraftingOrdersStatePacket View(CraftingOrderStore store, Player player, string message, bool open) => new()
    {
        OpenWindow = open, MessageKey = message,
        Aureons = (int)Math.Min(int.MaxValue, player.Items.Where(i => i.ItemId == Currency()?.Id).Sum(i => (long)i.Quantity)),
        PendingAureons = store.PendingAureons.GetValueOrDefault(player.Id),
        PendingItems = store.Deliveries.GetValueOrDefault(player.Id)?.Sum(i => (long)i.Quantity) ?? 0,
        CraftableItemIds = CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>().Select(r => r.ItemId)
            .Distinct().Where(Craftable).OrderBy(ItemDescriptor.GetName, StringComparer.OrdinalIgnoreCase).ToArray(),
        // Public orders cannot crowd out the player's accepted work and history.
        Orders = store.Orders.Where(o => o.CustomerId == player.Id || o.CrafterId == player.Id || o.Status == 0)
            .OrderBy(o => (o.CustomerId == player.Id || o.CrafterId == player.Id) && o.Status < 2 ? 0 : o.Status == 0 ? 1 : 2)
            .ThenBy(o => o.Status).ThenByDescending(o => o.Deadline).Take(500)
            .Select(o => new CraftingOrderView { Id = o.Id, ItemId = o.ItemId, Quantity = o.Quantity, Reward = o.Reward,
                Customer = o.Customer, Crafter = o.Crafter, Status = o.Status, IsCustomer = o.CustomerId == player.Id,
                IsCrafter = o.CrafterId == player.Id, DeadlineUtcMs = o.Deadline.ToUnixTimeMilliseconds() }).ToArray(),
    };
}
