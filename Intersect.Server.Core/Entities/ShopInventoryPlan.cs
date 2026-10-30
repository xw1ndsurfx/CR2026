using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database;

namespace Intersect.Server.Entities;

internal enum ShopPlanError { None, Invalid, NotEnough, NoSpace, Overflow }

// Build the entire exchange on detached copies. A failed quote never consumes items.
internal static class ShopInventoryPlan
{
    internal static ShopPlanError Purchase(Item[] inventory, ItemDescriptor product, ItemDescriptor currency,
        int price, int quantity, out Item[] result)
    {
        result = null;
        if (quantity < 1 || price < 0 || product == null || currency == null) return ShopPlanError.Invalid;
        var total = (long)price * quantity;
        if (total > int.MaxValue) return ShopPlanError.Overflow;
        var draft = inventory.Select(item => item?.Clone() ?? Item.None).ToArray();
        if (total > 0 && !Take(draft, currency, (int)total, Enumerable.Range(0, draft.Length))) return ShopPlanError.NotEnough;
        if (!Give(draft, product, quantity)) return ShopPlanError.NoSpace;
        result = draft; return ShopPlanError.None;
    }

    internal static ShopPlanError Sale(Item[] inventory, int selected, ItemDescriptor product, ItemDescriptor currency,
        int price, int quantity, bool allCopies, Func<int, bool> eligible, out Item[] result, out int sold)
    {
        result = null; sold = 0;
        if (selected < 0 || selected >= inventory.Length || product == null || inventory[selected]?.ItemId != product.Id ||
            price < 0 || (!allCopies && quantity < 1) || (price > 0 && currency == null)) return ShopPlanError.Invalid;
        var slots = Enumerable.Range(0, inventory.Length).Where(i => inventory[i]?.ItemId == product.Id && inventory[i].Quantity > 0 && eligible(i))
            .OrderBy(i => i == selected ? 0 : 1).ToArray();
        var available = slots.Sum(i => product.IsStackable ? (long)Math.Max(0, inventory[i].Quantity) : 1L);
        sold = allCopies ? (int)Math.Min(int.MaxValue, available) : quantity;
        if (sold < 1 || sold > available) return ShopPlanError.NotEnough;
        var total = (long)price * sold;
        if (total > int.MaxValue) return ShopPlanError.Overflow;
        var draft = inventory.Select(item => item?.Clone() ?? Item.None).ToArray();
        if (!Take(draft, product, sold, slots)) return ShopPlanError.NotEnough;
        if (total > 0 && !Give(draft, currency, (int)total)) return ShopPlanError.NoSpace;
        result = draft; return ShopPlanError.None;
    }

    private static bool Take(Item[] slots, ItemDescriptor descriptor, int count, IEnumerable<int> order)
    {
        foreach (var i in order)
        {
            if (slots[i].ItemId != descriptor.Id || slots[i].Quantity <= 0) continue;
            var units = descriptor.IsStackable ? Math.Max(0, slots[i].Quantity) : 1;
            var take = Math.Min(count, units); count -= take;
            if (take == units) slots[i] = Item.None;
            else slots[i].Quantity -= take;
            if (count == 0) return true;
        }
        return false;
    }

    private static bool Give(Item[] slots, ItemDescriptor descriptor, int count)
    {
        if (descriptor == null || count < 0) return false;
        var stack = descriptor.IsStackable ? Math.Max(1, descriptor.MaxInventoryStack) : 1;
        if (descriptor.IsStackable)
            foreach (var item in slots.Where(i => i.ItemId == descriptor.Id))
            {
                var added = (int)Math.Min(count, Math.Max(0L, (long)stack - item.Quantity));
                item.Quantity += added; count -= added;
                if (count == 0) return true;
            }
        for (var i = 0; i < slots.Length && count > 0; i++)
        {
            if (slots[i].ItemId != Guid.Empty) continue;
            var added = Math.Min(count, stack);
            // A separate Item rolls independent equipment stats for each non-stackable copy.
            slots[i] = new Item(descriptor.Id, added); count -= added;
        }
        return count == 0;
    }
}
