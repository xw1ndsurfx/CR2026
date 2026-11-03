using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Database;
namespace Intersect.Server.Jousting;
internal enum JoustExchangeResult { Success, Duplicate, Invalid, InventoryFull, SaveFailed }
internal static class JoustEconomyExchange
{
    // Caller owns EntityLock and the native account save gate. Save persists inventory and Pets together.
    internal static JoustExchangeResult Redeem(JoustCareer career, JoustOffer offer, ItemDescriptor item, IEnumerable<InventorySlot> inventory, Guid request, Func<bool> save)
    {
        if (request == Guid.Empty || !offer.IsValid || item.Id != offer.ItemId || !item.IsStackable || item.MaxInventoryStack < 1 || career.Medals < offer.Medals) return JoustExchangeResult.Invalid;
        if (career.Purchases.Contains(request)) return JoustExchangeResult.Duplicate;
        var plan = new List<(InventorySlot Slot, Item Before, Item After)>(); var left = offer.Quantity;
        foreach (var slot in inventory.OrderBy(s => s.ItemId == offer.ItemId ? 0 : 1))
        {
            if (left == 0) break;
            if (slot.BagId.GetValueOrDefault() != Guid.Empty || slot.Quantity < 0 || (slot.ItemId != Guid.Empty && slot.ItemId != offer.ItemId)) continue;
            var count = Math.Min(left, item.MaxInventoryStack - (slot.ItemId == Guid.Empty ? 0 : slot.Quantity));
            if (count <= 0) continue;
            var after = slot.ItemId == Guid.Empty ? new Item(offer.ItemId, count) : slot.Clone();
            if (slot.ItemId != Guid.Empty) after.Quantity += count;
            plan.Add((slot, slot.Clone(), after)); left -= count;
        }
        if (left != 0) return JoustExchangeResult.InventoryFull;
        var receipts = career.Purchases.ToList();
        foreach (var change in plan) change.Slot.Set(change.After);
        career.Medals -= offer.Medals; career.Purchases.Add(request);
        if (career.Purchases.Count > 256) career.Purchases.RemoveAt(0);
        try { if (save()) return JoustExchangeResult.Success; }
        catch { Rollback(); throw; }
        Rollback(); return JoustExchangeResult.SaveFailed;
        void Rollback() { career.Medals += offer.Medals; career.Purchases = receipts; foreach (var change in plan) change.Slot.Set(change.Before); }
    }
}
