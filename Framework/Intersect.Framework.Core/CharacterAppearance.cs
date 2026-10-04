using MessagePack;

namespace Intersect.Framework.Core;

/// <summary>
/// Inventory-independent base appearance for a player character.
/// Equipment may visually cover these layers without replacing the saved choice.
/// </summary>
[MessagePackObject]
public partial class CharacterAppearance
{
    public const string HairPrefix = "cc_hair_";
    public const string ShirtPrefix = "cc_shirt_";
    public const string PantsPrefix = "cc_pants_";
    public const string BootsPrefix = "cc_boots_";

    private static readonly string[] AnimationSuffixes =
    [
        "_attack.png",
        "_cast.png",
        "_idle.png",
        "_shoot.png",
        "_weapon.png",
    ];

    [Key(0)]
    public string HairStyle { get; set; } = string.Empty;

    [Key(1)]
    public Color HairColor { get; set; } = Color.White;

    [Key(2)]
    public string ShirtStyle { get; set; } = string.Empty;

    [Key(3)]
    public Color ShirtColor { get; set; } = Color.White;

    [Key(4)]
    public string PantsStyle { get; set; } = string.Empty;

    [Key(5)]
    public Color PantsColor { get; set; } = Color.White;

    [Key(6)]
    public string BootsStyle { get; set; } = string.Empty;

    [Key(7)]
    public Color BootsColor { get; set; } = Color.White;

    public CharacterAppearance SanitizedCopy()
    {
        return new CharacterAppearance
        {
            HairStyle = SanitizeStyle(HairStyle, HairPrefix),
            HairColor = Opaque(HairColor),
            ShirtStyle = SanitizeStyle(ShirtStyle, ShirtPrefix),
            ShirtColor = Opaque(ShirtColor),
            PantsStyle = SanitizeStyle(PantsStyle, PantsPrefix),
            PantsColor = Opaque(PantsColor),
            BootsStyle = SanitizeStyle(BootsStyle, BootsPrefix),
            BootsColor = Opaque(BootsColor),
        };
    }

    public bool TryGetLayer(string paperdollSlot, out string style, out Color color)
    {
        style = string.Empty;
        color = Color.White;

        if (string.Equals(paperdollSlot, "Head", StringComparison.OrdinalIgnoreCase))
        {
            style = HairStyle;
            color = HairColor ?? Color.White;
        }
        else if (string.Equals(paperdollSlot, "Armor", StringComparison.OrdinalIgnoreCase))
        {
            style = ShirtStyle;
            color = ShirtColor ?? Color.White;
        }
        else if (string.Equals(paperdollSlot, "Legs", StringComparison.OrdinalIgnoreCase))
        {
            style = PantsStyle;
            color = PantsColor ?? Color.White;
        }
        else if (string.Equals(paperdollSlot, "Boots", StringComparison.OrdinalIgnoreCase))
        {
            style = BootsStyle;
            color = BootsColor ?? Color.White;
        }

        return !string.IsNullOrWhiteSpace(style);
    }

    public static bool IsCatalogStyle(string? style, string expectedPrefix)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return true;
        }

        var value = style.Trim();
        if (value.Length > 128 ||
            value.Contains('/') ||
            value.Contains('\\') ||
            value.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (!value.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !AnimationSuffixes.Any(suffix =>
            value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeStyle(string? style, string expectedPrefix)
    {
        var value = style?.Trim() ?? string.Empty;
        return IsCatalogStyle(value, expectedPrefix) ? value : string.Empty;
    }

    private static Color Opaque(Color? color)
    {
        return color == null
            ? Color.White
            : new Color(255, color.R, color.G, color.B);
    }
}
