namespace Intersect.Framework.Core.GameObjects.Items;

public static class ShopPriceSuggestion
{
    // Item.Price is the existing economy value; do not guess a new rarity economy.
    // A free/unvalued currency has no meaningful conversion rate.
    public static int? Suggest(ItemDescriptor? item, ItemDescriptor? currency, bool shopBuys)
    {
        if (item == null || currency == null || item.Id == currency.Id || item.Price <= 0 || currency.Price <= 0) return null;
        var value = item.Price / (decimal)currency.Price;
        return (int)Math.Clamp(shopBuys ? decimal.Floor(value * 0.5m) : decimal.Ceiling(value), 1, int.MaxValue);
    }
}
