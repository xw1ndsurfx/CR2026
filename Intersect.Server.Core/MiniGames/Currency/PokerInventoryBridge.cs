#nullable enable
using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.MiniGames.Currency;

/// <summary>
/// The only funded-poker code that changes inventory. Lock order is EntityLock ->
/// User.PokerSaveGate -> runtime -> ledger. Live slots change only AFTER commit resolves.
/// No bank, bag, equipment, item price, trade flag or test balance is used as a wallet.
/// </summary>
internal static class PokerInventoryBridge
{
    private sealed record Change(InventorySlot Slot, Item Value);
    private static readonly object InitGate = new();
    private static PokerMoneyLedger? _ledger;
    private static long _lastError;
    private static readonly Dictionary<Guid, long> RefundNotices = new();

    internal static PokerMoneyLedger Ledger
    {
        get
        {
            lock (InitGate)
            {
                if (_ledger != null) return _ledger;
                using var context = DbInterface.CreatePlayerContext(readOnly: false);
                if (context.Database.GetDbConnection() is not SqliteConnection connection)
                    throw new NotSupportedException("Funded poker currently requires the SQLite player database. No items were taken.");
                _ledger = new PokerMoneyLedger(connection.ConnectionString);
                return _ledger;
            }
        }
    }

    internal static MoneySeat BuyIn(Player player, Guid seat, Guid table, Guid currency, string house, long amount)
    {
        if (player.User == null) throw new MoneyRuleException("No authenticated account.");
        lock (player.EntityLock)
        lock (player.User.PokerSaveGate)
        {
            var item = ItemDescriptor.Get(currency);
            if (!MiniGameCurrency.IsCompatible(item)) throw new MoneyRuleException("The table currency is missing or incompatible.");
            var changes = DebitPlan(player, currency, amount);
            var result = Ledger.OpenHuman(seat, table, player.Id, currency, house, amount,
                (connection, transaction) => SavePlan(player, changes, connection, transaction));
            Apply(changes);
            return result;
        }
    }

    private static List<Change> DebitPlan(Player player, Guid currency, long amount)
    {
        var changes = new List<Change>(); var remaining = amount;
        foreach (var slot in player.Items)
        {
            if (remaining == 0) break;
            if (slot.Id == Guid.Empty || slot.ItemId != currency || slot.Quantity <= 0 || slot.BagId.GetValueOrDefault() != Guid.Empty) continue;
            var count = (int)Math.Min(remaining, slot.Quantity);
            var next = slot.Clone(); next.Quantity -= count;
            if (next.Quantity == 0) next = Item.None;
            changes.Add(new(slot, next)); remaining -= count;
        }
        if (remaining != 0) throw new MoneyRuleException($"Not enough {ItemDescriptor.GetName(currency)} in inventory. Buy-in: {amount}.");
        return changes;
    }

    private static List<Change> CreditPlan(Player player, Guid currency, long amount)
    {
        var item = ItemDescriptor.Get(currency);
        if (!MiniGameCurrency.IsCompatible(item)) throw new MoneyRuleException("Refund waiting: restore the original currency item definition.");
        var changes = new List<Change>(); var remaining = amount;
        foreach (var slot in player.Items)
        {
            if (remaining == 0) break;
            if (slot.Id == Guid.Empty || slot.ItemId != currency || slot.Quantity < 0 ||
                slot.BagId.GetValueOrDefault() != Guid.Empty || slot.Quantity >= item.MaxInventoryStack) continue;
            var count = (int)Math.Min(remaining, (long)item.MaxInventoryStack - slot.Quantity);
            var next = slot.Clone(); next.Quantity = checked(next.Quantity + count);
            changes.Add(new(slot, next)); remaining -= count;
        }
        foreach (var slot in player.Items)
        {
            if (remaining == 0) break;
            if (slot.Id == Guid.Empty || slot.ItemId != Guid.Empty || slot.BagId.GetValueOrDefault() != Guid.Empty) continue;
            var count = (int)Math.Min(remaining, item.MaxInventoryStack);
            changes.Add(new(slot, new Item(currency, count))); remaining -= count;
        }
        if (remaining != 0) throw new MoneyRuleException("Refund waiting: make room in your inventory. Nothing has been dropped or lost.");
        return changes;
    }

    private static void SavePlan(Player player, IReadOnlyList<Change> changes, SqliteConnection connection, SqliteTransaction transaction)
    {
        using var context = DbInterface.CreatePlayerContext(readOnly: false);
        context.Database.SetDbConnection(connection, contextOwnsConnection: false);
        using var enlistment = context.Database.UseTransaction(transaction);
        foreach (var change in changes)
        {
            // Load a detached-from-live tracked copy. Do not attach/save the player's graph.
            var persisted = context.Player_Items.SingleOrDefault(s => s.Id == change.Slot.Id && s.PlayerId == player.Id)
                ?? throw new MoneyRuleException("An inventory slot is not saved yet; try again after the next save.");
            persisted.Set(change.Value);
        }
        context.SaveChanges(); // Still uncommitted: ledger will commit inventory + receipt together.
    }
    private static void Apply(IEnumerable<Change> changes)
    { foreach (var change in changes) change.Slot.Set(change.Value); }

    internal static void NotifyInventory(Player player)
    {
        try { if (player.Client != null) PacketSender.SendInventory(player); }
        catch (Exception error) { Log(error); }
    }
    internal static void Recover(Player player)
    {
        if (player.User == null || player.Client is not { IsEditor: false }) return;
        var changed = false;
        lock (player.EntityLock)
        lock (player.User.PokerSaveGate)
        {
            if (!player.IsOnline || player.Client == null || player.IsSaving) return;
            foreach (var refund in Ledger.Refunds(player.Id))
            {
                try
                {
                    List<Change> changes = [];
                    Ledger.Cashout(refund, (connection, transaction) =>
                    {
                        changes = CreditPlan(player, refund.Currency, refund.Amount);
                        SavePlan(player, changes, connection, transaction);
                    });
                    Apply(changes); changed |= changes.Count > 0;
                    if (refund.Amount > 0) PacketSender.SendChatMsg(player,
                        $"[Poker] Returned {refund.Amount} {ItemDescriptor.GetName(refund.Currency)} to your inventory.",
                        ChatMessageType.Inventory, Color.White);
                }
                catch (MoneyRuleException error)
                {
                    lock (RefundNotices)
                    {
                        if (Environment.TickCount64 - RefundNotices.GetValueOrDefault(player.Id, -60_000) >= 60_000)
                        {
                            RefundNotices[player.Id] = Environment.TickCount64;
                            PacketSender.SendChatMsg(player, "[Poker] " + error.Message, ChatMessageType.Inventory, Color.White);
                        }
                    }
                }
            }
        }
        if (changed) NotifyInventory(player);
    }
    internal static void Log(Exception error)
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastError) < 30_000) return;
        Interlocked.Exchange(ref _lastError, now);
        ApplicationContext.Context.Value?.Logger.LogError(error,
            "Funded poker unavailable. Escrow is retained; do not delete the PokerMoney tables or player database.");
    }
}
