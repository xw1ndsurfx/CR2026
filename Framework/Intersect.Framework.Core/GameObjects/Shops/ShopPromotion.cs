namespace Intersect.GameObjects;

public sealed class ShopPromotion
{
    public bool Enabled { get; set; }
    public int DiscountPercent { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public bool IsValid => DiscountPercent is >= 1 and <= 99 && EndsAtUtc > StartsAtUtc;
    public bool IsActive(DateTimeOffset now) => Enabled && IsValid && now >= StartsAtUtc && now < EndsAtUtc;

    public static int EffectiveDiscount(DateTimeOffset now, ShopPromotion? promotion) =>
        promotion?.IsActive(now) == true ? promotion.DiscountPercent : 0;

    public static int Price(int original, int discount) => original <= 0 ? 0 :
        (int)Math.Max(1, ((long)original * (100 - Math.Clamp(discount, 0, 99)) + 99) / 100);
}
