using System.ComponentModel;

namespace Intersect.Framework.Core.MiniGames.Lockpicking;

public sealed class LockpickingLootEntry
{
    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid ItemId { get; set; }

    [DefaultValue(1)]
    public int MinQuantity { get; set; } = 1;

    [DefaultValue(1)]
    public int MaxQuantity { get; set; } = 1;

    [DefaultValue(100)]
    public int BaseChancePercent { get; set; } = 100;

    /// <summary>
    /// Additional chance in basis points (1/100 of a percent) per profession level
    /// above this entry's minimum profession level.
    /// </summary>
    [DefaultValue(25)]
    public int ChanceBonusBasisPointsPerLevel { get; set; } = 25;

    [DefaultValue(10)]
    public int PerfectBonusPercent { get; set; } = 10;

    [DefaultValue(0)]
    public int MinimumProfessionLevel { get; set; }

    /// <summary>
    /// Adds one item to the rolled quantity for each N profession levels.
    /// Zero disables quantity scaling.
    /// </summary>
    [DefaultValue(0)]
    public int QuantityBonusEveryLevels { get; set; }

    public bool IsValid =>
        ItemId != Guid.Empty &&
        MinQuantity is >= 1 and <= 1_000_000 &&
        MaxQuantity >= MinQuantity && MaxQuantity <= 1_000_000 &&
        BaseChancePercent is >= 0 and <= 100 &&
        ChanceBonusBasisPointsPerLevel is >= 0 and <= 10_000 &&
        PerfectBonusPercent is >= 0 and <= 500 &&
        MinimumProfessionLevel is >= 0 and <= 500 &&
        QuantityBonusEveryLevels is >= 0 and <= 500;
}
