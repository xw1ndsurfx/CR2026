using Intersect.Framework.Core;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class RoyalStylistStatePacket : IntersectPacket
{
    public RoyalStylistStatePacket()
    {
    }

    public RoyalStylistStatePacket(
        Guid stylistId,
        string stylistName,
        string description,
        CharacterAppearance appearance,
        string[] hairStyles,
        string[] shirtStyles,
        string[] pantsStyles,
        string[] bootsStyles,
        Guid currencyItemId,
        string currencyName,
        int price,
        int balance,
        bool premiumRequired,
        bool premiumActive,
        bool canApply,
        bool allowNoHair,
        bool allowNoShirt,
        bool allowNoPants,
        bool allowNoBoots,
        string message = ""
    )
    {
        StylistId = stylistId;
        StylistName = stylistName;
        Description = description;
        Appearance = appearance ?? new CharacterAppearance();
        HairStyles = hairStyles ?? [];
        ShirtStyles = shirtStyles ?? [];
        PantsStyles = pantsStyles ?? [];
        BootsStyles = bootsStyles ?? [];
        CurrencyItemId = currencyItemId;
        CurrencyName = currencyName;
        Price = price;
        Balance = balance;
        PremiumRequired = premiumRequired;
        PremiumActive = premiumActive;
        CanApply = canApply;
        AllowNoHair = allowNoHair;
        AllowNoShirt = allowNoShirt;
        AllowNoPants = allowNoPants;
        AllowNoBoots = allowNoBoots;
        Message = message;
    }

    [Key(0)] public Guid StylistId { get; set; }
    [Key(1)] public string StylistName { get; set; } = string.Empty;
    [Key(2)] public string Description { get; set; } = string.Empty;
    [Key(3)] public CharacterAppearance Appearance { get; set; } = new();
    [Key(4)] public string[] HairStyles { get; set; } = [];
    [Key(5)] public string[] ShirtStyles { get; set; } = [];
    [Key(6)] public string[] PantsStyles { get; set; } = [];
    [Key(7)] public string[] BootsStyles { get; set; } = [];
    [Key(8)] public Guid CurrencyItemId { get; set; }
    [Key(9)] public string CurrencyName { get; set; } = string.Empty;
    [Key(10)] public int Price { get; set; }
    [Key(11)] public int Balance { get; set; }
    [Key(12)] public bool PremiumRequired { get; set; }
    [Key(13)] public bool PremiumActive { get; set; }
    [Key(14)] public bool CanApply { get; set; }
    [Key(15)] public string Message { get; set; } = string.Empty;
    [Key(16)] public bool AllowNoHair { get; set; }
    [Key(17)] public bool AllowNoShirt { get; set; }
    [Key(18)] public bool AllowNoPants { get; set; }
    [Key(19)] public bool AllowNoBoots { get; set; }
}
