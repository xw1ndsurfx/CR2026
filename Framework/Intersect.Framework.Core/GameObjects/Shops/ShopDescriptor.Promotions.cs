using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Intersect.GameObjects;

public partial class ShopDescriptor
{
    private DateTimeOffset? _serverPricingTime;
    private long _pricingClock;
    [NotMapped, JsonIgnore] public DateTimeOffset PricingTime => _serverPricingTime is { } server
        ? server + Stopwatch.GetElapsedTime(_pricingClock) : DateTimeOffset.UtcNow;

    public void SynchronizePricingTime(long unixMilliseconds)
    {
        _serverPricingTime = unixMilliseconds > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds) : null;
        _pricingClock = Stopwatch.GetTimestamp();
    }

    public int SellingPrice(ShopItemDescriptor offer, DateTimeOffset now) =>
        ShopPromotion.Price(offer.CostItemQuantity, ShopPromotion.EffectiveDiscount(now, offer.Promotion));

    public int ServicePrice(ShopServiceOffer offer, DateTimeOffset now) =>
        ShopPromotion.Price(offer.Price, ShopPromotion.EffectiveDiscount(now, offer.Promotion));

    public DateTimeOffset NextPromotionChange(DateTimeOffset now) =>
        SellingItems.Select(offer => offer.Promotion).Concat(ServiceOffers.Select(offer => offer.Promotion))
            .Where(promotion => promotion is { Enabled: true, IsValid: true })
            .SelectMany(promotion => new[] { promotion!.StartsAtUtc, promotion.EndsAtUtc })
            .Where(time => time > now).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();

    public bool TryPurchaseTotal(ShopItemDescriptor offer, int amount, DateTimeOffset now, int? expectedPrice, out int total)
    {
        total = 0;
        var price = SellingPrice(offer, now);
        if (amount <= 0 || offer.CostItemQuantity < 0 || (expectedPrice.HasValue && expectedPrice != price)) return false;
        var value = (long)price * amount;
        if (value > int.MaxValue) return false;
        total = (int)value;
        return true;
    }
}
