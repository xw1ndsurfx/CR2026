namespace Intersect.Framework.Core.Archery;

/// <summary>Presentation settings shared by the editor and the combat HUD.</summary>
public sealed class WeaponSkillBarSettings
{
    public bool Enabled { get; set; } = true;
    public int SlotSize { get; set; } = 34;
    public int Gap { get; set; } = 3;
    public int MaximumColumns { get; set; } = 10;
    public int HotbarSpacing { get; set; } = 4;
    public bool IsValid => SlotSize is >= 24 and <= 64 && Gap is >= 0 and <= 12 &&
        MaximumColumns is >= 1 and <= 16 && HotbarSpacing is >= 0 and <= 48;
}
