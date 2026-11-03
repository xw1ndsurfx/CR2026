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
    public WeaponSkillDefinition Skill { get; set; } = new();
    public bool Configured => Skill.Enabled || DamagePercentPerRank > 0 || AttackSpeedPercentPerRank > 0 || CritChancePerRank > 0 || RankSpellIds.Length > 0;
    public static MasteryTalent[] ArcheryDefaults() => [
        new() { DamagePercentPerRank = .5 },
        new() { Key = "quick-shot", Name = "Quick Shot", RequiredLevel = 3, AttackSpeedPercentPerRank = .5 },
        new() { Key = "piercing-arrow", Name = "Piercing Arrow", RequiredLevel = 5, Prerequisite = "steady-aim" },
        new() { Key = "multi-shot", Name = "Multi Shot", RequiredLevel = 10, Prerequisite = "quick-shot" },
        new() { Key = "rain-of-arrows", Name = "Rain of Arrows", RequiredLevel = 20, Prerequisite = "multi-shot" },
        new() { Key = "eagle-eye", Name = "Eagle Eye", RequiredLevel = 25, CritChancePerRank = 1, Prerequisite = "steady-aim", PrerequisiteRank = 3 }
    ];

    public static MasteryTalent[] WeaponDefaults(string category, int cap = 30)
    {
        if (category == "archery") return ArcheryDefaults();
        var names = category switch
        {
            "swords" => new[] { "Sword Training", "Quick Slashes", "Blade Precision", "Heavy Slashes", "Relentless Blade", "Sword Expertise" },
            "daggers" => new[] { "Dagger Training", "Quick Stabs", "Vital Strikes", "Deep Cuts", "Relentless Daggers", "Dagger Expertise" },
            "axes" => new[] { "Heavy Weapon Training", "Swift Swings", "Crushing Precision", "Heavy Blows", "Relentless Swings", "Heavy Weapon Expertise" },
            "magic" => new[] { "Staff Training", "Quick Casting", "Arcane Precision", "Arcane Power", "Arcane Rhythm", "Staff Expertise" },
            "whips" => new[] { "Whip Training", "Quick Lashes", "Lash Precision", "Heavy Lashes", "Relentless Lashes", "Whip Expertise" },
            _ => [],
        };
        if (names.Length == 0) return [];
        // Every node has a real effect. Native active techniques use animations
        // and their own cooldowns, independently of learned spells.
        var levels = new[] { 1, 3, 5, 10, 20, 25 };
        var keys = new[] { "training", "swiftness", "precision", "power", "rhythm", "expertise" };
        return names.Select((name, i) => new MasteryTalent
        {
            Key = keys[i], Name = name, RequiredLevel = Math.Min(levels[i], cap),
            DamagePercentPerRank = i == 0 ? .5 : 0,
            AttackSpeedPercentPerRank = 0,
            CritChancePerRank = i is 2 or 5 ? 1 : 0,
            Skill = new WeaponSkillDefinition
            {
                Enabled=i is 1 or 3 or 4, Range=category=="magic"?6:category=="whips"?3:1,
                CooldownMilliseconds=i==1?1500:i==3?4000:8000,
                DamagePercent=i==1?110:i==3?160:220,
            },
            Prerequisite = i switch { 2 => keys[0], 3 => keys[0], 4 => keys[1], 5 => keys[2], _ => "" },
            PrerequisiteRank = i == 5 ? 3 : 1,
        }).ToArray();
    }
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
            t.Skill is not { IsValid:true } || t.Skill.Enabled && t.RankSpellIds.Length>0 ||
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
