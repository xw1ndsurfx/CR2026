namespace Intersect.Framework.Core.Exportation;

/// <summary>Allowed resource keys from the approved 4x3 pixel-art atlas.</summary>
public static class ExportationArt
{
    public const string Background = "exportation/background.png";
    public static readonly string[] Goods = ["planches", "lingots_fer", "potion_rouge", "tissu", "pommes"];
    public static bool IsGoodsSprite(string? sprite) => string.IsNullOrEmpty(sprite) ||
        Goods.Contains(sprite, StringComparer.Ordinal);
}
