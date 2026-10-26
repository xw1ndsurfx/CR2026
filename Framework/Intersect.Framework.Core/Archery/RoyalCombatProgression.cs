namespace Intersect.Framework.Core.Archery;

public sealed class MasteryTalent
{
    public string Key { get; set; } = "steady-aim";
    public string Name { get; set; } = "Steady Aim";
    public int RequiredLevel { get; set; } = 1;
    public int MaxRank { get; set; } = 5;
    public string Prerequisite { get; set; } = "";
    public int PrerequisiteRank { get; set; } = 1;
    public double DamagePercentPerRank { get; set; }
    public double AttackSpeedPercentPerRank { get; set; }
    public int CritChancePerRank { get; set; }
    [System.ComponentModel.Browsable(false)] public Guid[] RankSpellIds { get; set; } = [];
    public bool Configured => DamagePercentPerRank > 0 || AttackSpeedPercentPerRank > 0 || CritChancePerRank > 0 || RankSpellIds.Length > 0;
    public static MasteryTalent[] ArcheryDefaults() => [
        new() { DamagePercentPerRank = .5 },
        new() { Key = "quick-shot", Name = "Quick Shot", RequiredLevel = 3, AttackSpeedPercentPerRank = .5 },
        new() { Key = "piercing-arrow", Name = "Piercing Arrow", RequiredLevel = 5, Prerequisite = "steady-aim" },
        new() { Key = "multi-shot", Name = "Multi Shot", RequiredLevel = 10, Prerequisite = "quick-shot" },
        new() { Key = "rain-of-arrows", Name = "Rain of Arrows", RequiredLevel = 20, Prerequisite = "multi-shot" },
        new() { Key = "eagle-eye", Name = "Eagle Eye", RequiredLevel = 25, CritChancePerRank = 1, Prerequisite = "steady-aim", PrerequisiteRank = 3 }
    ];
}

public sealed class WeaponAmmunitionRule
{
    public Guid WeaponId { get; set; }
    // Existing projectile definitions carry the ammo ID, quantity, trajectory and on-hit effects.
    public Guid[] ProjectileIds { get; set; } = [];
}

public static class MasteryRules
{
    public static bool Key(string? key) => key is { Length: > 0 and <= 32 } && key.All(c => c is >= 'a' and <= 'z' or '-');
    public static int Points(MasteryCategory category, long experience) => (category.Level(experience) - 1) * category.PointsPerLevel;
    public static int Spent(MasteryCategory category, IReadOnlyDictionary<string, int> ranks) =>
        category.Talents.Sum(t => Math.Clamp(ranks.GetValueOrDefault(t.Key), 0, t.MaxRank));
    public static bool Eligible(MasteryCategory category, MasteryTalent talent, long experience, IReadOnlyDictionary<string, int> ranks) =>
        category.Level(experience) >= talent.RequiredLevel && Spent(category, ranks) <= Points(category, experience) &&
        (talent.Prerequisite.Length == 0 || ranks.GetValueOrDefault(talent.Prerequisite) >= talent.PrerequisiteRank);
    public static bool CanBuy(MasteryCategory category, MasteryTalent talent, long experience, IReadOnlyDictionary<string, int> ranks) =>
        talent.Configured && Eligible(category, talent, experience, ranks) && ranks.GetValueOrDefault(talent.Key) < talent.MaxRank &&
        Spent(category, ranks) < Points(category, experience);
    public static bool ValidTree(MasteryCategory category)
    {
        if (category.PointsPerLevel is < 0 or > 5 || category.Talents is not { Length: <= 16 }) return false;
        var talents = category.Talents;
        if (talents.Any(t => t == null || !Key(t.Key) || t.Name is not { Length: > 0 and <= 80 } ||
            t.RequiredLevel < 1 || t.RequiredLevel > category.Cap || t.MaxRank is < 1 or > 5 ||
            !double.IsFinite(t.DamagePercentPerRank) || t.DamagePercentPerRank is < 0 or > 2 ||
            !double.IsFinite(t.AttackSpeedPercentPerRank) || t.AttackSpeedPercentPerRank is < 0 or > 2 ||
            t.CritChancePerRank is < 0 or > 2 || t.RankSpellIds == null ||
            t.RankSpellIds.Length != 0 && (t.RankSpellIds.Length != t.MaxRank || t.RankSpellIds.Any(id => id == Guid.Empty)) ||
            t.Prerequisite == null || t.PrerequisiteRank is < 1 or > 5) ||
            talents.Select(t => t.Key).Distinct().Count() != talents.Length) return false;
        foreach (var talent in talents)
        {
            var seen = new HashSet<string> { talent.Key }; var node = talent;
            while (node.Prerequisite.Length > 0)
            {
                var parent = talents.FirstOrDefault(t => t.Key == node.Prerequisite);
                if (parent == null || parent.RequiredLevel > node.RequiredLevel || node.PrerequisiteRank > parent.MaxRank || !seen.Add(parent.Key)) return false;
                node = parent;
            }
        }
        return true;
    }
}

public sealed class AmmunitionView
{
    public Guid WeaponId { get; set; }
    public Guid SelectedItemId { get; set; }
    public AmmunitionEntry[] Entries { get; set; } = [];
}
public sealed class AmmunitionEntry
{
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public bool Compatible { get; set; }
    public int Required { get; set; }
}
