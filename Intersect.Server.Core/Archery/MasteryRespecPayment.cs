using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;

namespace Intersect.Server.Archery;

public sealed class PaidMasteryRespec
{
    public Guid RequestId { get; set; }
    public string Category { get; set; } = "";
    public Guid CurrencyId { get; set; }
    public int Cost { get; set; }
}
internal static class MasteryRespecPayment
{
    // Caller owns EntityLock and the native account save gate; save includes the pending receipt.
    internal static bool Debit(IEnumerable<InventorySlot> inventory, Guid currency, int cost, Func<bool> save)
    {
        if (currency == Guid.Empty || cost <= 0) return false;
        var plan = new List<(InventorySlot Slot, Item Before, Item After)>(); var left = cost;
        foreach (var slot in inventory)
        {
            if (left == 0) break;
            if (slot.ItemId != currency || slot.Quantity <= 0 || slot.BagId.GetValueOrDefault() != Guid.Empty) continue;
            var take = Math.Min(left, slot.Quantity); var next = slot.Clone(); next.Quantity -= take;
            plan.Add((slot, slot.Clone(), next.Quantity == 0 ? Item.None : next)); left -= take;
        }
        if (left != 0) return false;
        foreach (var change in plan) change.Slot.Set(change.After);
        try { if (save()) return true; }
        catch { Rollback(); throw; }
        Rollback(); return false;
        void Rollback() { foreach (var change in plan) change.Slot.Set(change.Before); }
    }
}
