using System.Text.Json;

namespace Intersect.Framework.Core.LogiCoins;

public enum LogiCoinOfferType
{
    Item = 0,
    Bundle = 1,
    Premium = 2,
}

public sealed record LogiCoinBundleItem(Guid ItemId, int Quantity)
{
    public bool IsStructurallyValid =>
        ItemId != Guid.Empty &&
        Quantity is >= 1 and <= 1_000_000_000;
}

public sealed class LogiCoinShopOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Offer";

    public string Description { get; set; } = string.Empty;

    public LogiCoinOfferType Type { get; set; }

    public int PriceLogiCoins { get; set; }

    public Guid ItemId { get; set; }

    public int ItemQuantity { get; set; } = 1;

    public LogiCoinBundleItem[] Bundle { get; set; } = [];

    public int PremiumDays { get; set; } = 30;

    public bool Enabled { get; set; } = true;

    public int SortOrder { get; set; }

    public int DiscountPercent { get; set; }

    public DateTimeOffset? PromotionStartsAtUtc { get; set; }

    public DateTimeOffset? PromotionEndsAtUtc { get; set; }

    public bool IsPromotionActive(DateTimeOffset now)
    {
        if (DiscountPercent <= 0)
        {
            return false;
        }

        if (PromotionStartsAtUtc is { } starts && now < starts)
        {
            return false;
        }

        if (PromotionEndsAtUtc is { } ends && now > ends)
        {
            return false;
        }

        return true;
    }

    public int EffectivePrice(DateTimeOffset now)
    {
        if (!IsPromotionActive(now))
        {
            return PriceLogiCoins;
        }

        var discounted = (long)PriceLogiCoins * (100 - DiscountPercent);
        return (int)Math.Max(0, (discounted + 99) / 100);
    }

    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 120 &&
        Description.Length <= 800 &&
        PriceLogiCoins is >= 0 and <= 1_000_000_000 &&
        DiscountPercent is >= 0 and <= 100 &&
        SortOrder is >= -1_000_000 and <= 1_000_000 &&
        (PromotionStartsAtUtc == null || PromotionEndsAtUtc == null || PromotionStartsAtUtc <= PromotionEndsAtUtc) &&
        Type switch
        {
            LogiCoinOfferType.Item =>
                ItemId != Guid.Empty &&
                ItemQuantity is >= 1 and <= 1_000_000_000,
            LogiCoinOfferType.Bundle =>
                Bundle is { Length: >= 1 and <= 128 } &&
                Bundle.All(entry => entry is { IsStructurallyValid: true }),
            LogiCoinOfferType.Premium =>
                PremiumDays is >= 1 and <= 3650,
            _ => false,
        };
}

public sealed class LogiCoinShopConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static LogiCoinShopConfiguration Instance { get; private set; } = new();

    public bool Enabled { get; set; } = true;

    public string ShopUrl { get; set; } = "https://shop.logiklik.com";

    /// <summary>
    /// Integer User Variable containing the premium expiry as Unix time milliseconds.
    /// Event conditions can compare this variable to System Time.
    /// </summary>
    public Guid PremiumUntilUserVariableId { get; set; }

    public LogiCoinShopOffer[] Offers { get; set; } = [];

    public bool IsStructurallyValid =>
        Uri.TryCreate(ShopUrl, UriKind.Absolute, out var shopUri) &&
        shopUri.Scheme is "https" or "http" &&
        (Offers ?? []).Length <= 512 &&
        (Offers ?? []).All(offer => offer is { IsStructurallyValid: true }) &&
        (Offers ?? []).Select(offer => offer.Id).Distinct().Count() == (Offers ?? []).Length;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static LogiCoinShopConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new LogiCoinShopConfiguration()
            : JsonSerializer.Deserialize<LogiCoinShopConfiguration>(json, JsonOptions) ?? new LogiCoinShopConfiguration();

        value.Offers ??= [];

        if (!value.IsStructurallyValid)
        {
            throw new InvalidDataException("Invalid LogiCoin Shop configuration.");
        }

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
