namespace Intersect.Framework.Core.GameObjects.Items;

public static class ShopPriceSuggestion
{
    // Item.Price is the existing economy value; do not guess a new rarity economy.
    // Unvalued currencies use the item price as editable base currency units.
    public static int? Suggest(ItemDescriptor? item, ItemDescriptor? currency, bool shopBuys)
    {
        if (item == null || currency == null || item.Id == currency.Id || item.Price <= 0) return null;
        var value = item.Price / (decimal)Math.Max(1, currency.Price);
        return (int)Math.Clamp(shopBuys ? decimal.Floor(value * 0.5m) : decimal.Ceiling(value), 1, int.MaxValue);
    }
}
