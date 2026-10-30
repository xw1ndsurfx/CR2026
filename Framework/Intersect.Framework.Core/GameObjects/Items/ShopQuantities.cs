namespace Intersect.Framework.Core.GameObjects.Items;

public static class ShopQuantities
{
    public static int Count(IEnumerable<(Guid ItemId, int Quantity)> inventory, ItemDescriptor item) => (int)Math.Min(int.MaxValue,
        inventory.Where(slot => slot.ItemId == item.Id && slot.Quantity > 0).Sum(slot => item.IsStackable ? (long)slot.Quantity : 1L));

    public static int MaximumPurchase(IEnumerable<(Guid ItemId, int Quantity)> inventory, ItemDescriptor product, ItemDescriptor currency, int price)
    {
        if (product == null || currency == null || price < 0) return 0;
        var slots = inventory.ToArray();
        var stack = product.IsStackable ? Math.Max(1, product.MaxInventoryStack) : 1;
        var candidate = price == 0 ? (int)Math.Min(int.MaxValue, (long)slots.Length * stack)
            : Math.Min(Count(slots, currency) / price, int.MaxValue / Math.Max(1, price));
        while (candidate > 0)
        {
            long cost = (long)price * candidate, capacity = 0;
            foreach (var slot in slots)
            {
                var id = slot.ItemId;
                var quantity = id == Guid.Empty ? 0 : id == currency.Id && !currency.IsStackable ? 1 : Math.Max(0, slot.Quantity);
                if (id == currency.Id && cost > 0)
                {
                    var taken = Math.Min(cost, quantity); cost -= taken; quantity -= (int)taken;
                    if (quantity == 0) id = Guid.Empty;
                }
                if (id == Guid.Empty) capacity += stack;
                else if (product.IsStackable && id == product.Id) capacity += Math.Max(0L, (long)stack - quantity);
            }
            if (capacity >= candidate) return candidate;
            candidate = (int)Math.Max(0, capacity);
        }
        return 0;
    }
}
