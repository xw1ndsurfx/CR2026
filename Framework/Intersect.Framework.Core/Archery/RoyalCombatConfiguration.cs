using System.Text.Json;

namespace Intersect.Framework.Core.Archery;

public sealed class MasteryCategory
{
    public string Key { get; set; } = "archery";
    public string Name { get; set; } = "Archery";
    public Guid[] WeaponIds { get; set; } = [];
    public int Cap { get; set; } = 30;
    public int BaseExperience { get; set; } = 100;
    public double DamagePercentPerLevel { get; set; } = .5;
    public int PointsPerLevel { get; set; } = 1;
    [System.ComponentModel.Browsable(false)] public MasteryTalent[] Talents { get; set; } = [];
    public long Required(int level) => (long)BaseExperience * level * level;
    public long Maximum => Enumerable.Range(1, Cap - 1).Sum(Required);
    public int Level(long total)
    {
        var level = 1;
        while (level < Cap && total >= Required(level)) { total -= Required(level); level++; }
        return level;
    }
    public long WithinLevel(long total)
    {
        for (var level = 1; level < Cap; level++)
        { if (total < Required(level)) return total; total -= Required(level); }
        return 0;
    }
}

public sealed class ArcheryRange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Royal Training Grounds";
    public Guid MapId { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public bool Enabled { get; set; }
    public double Wind { get; set; }
    [System.ComponentModel.Browsable(false)] public ArcheryRules Rules { get; set; } = new();
}

public sealed class RoyalCombatConfiguration
{
    public MasteryCategory[] Masteries { get; set; } = [new() { Talents = MasteryTalent.ArcheryDefaults() },
        new() { Key = "swords", Name = "Swordsmanship" }, new() { Key = "daggers", Name = "Daggers" },
        new() { Key = "axes", Name = "Axes & Hammers" }, new() { Key = "magic", Name = "Staves & Magic" }];
    public ArcheryRange[] Ranges { get; set; } = [];
    public WeaponAmmunitionRule[] Ammunition { get; set; } = [];
    public bool IsValid => Masteries is { Length: > 0 and <= 32 } && Ranges is { Length: <= 64 } &&
        Masteries.All(m => m != null && m.Key is { Length: > 0 and <= 32 } && m.Key.All(c => c is >= 'a' and <= 'z' or '-') &&
            m.Name is { Length: > 0 and <= 80 } && m.Cap is >= 2 and <= 100 && m.BaseExperience is >= 1 and <= 100000 &&
            double.IsFinite(m.DamagePercentPerLevel) && m.DamagePercentPerLevel is >= 0 and <= 2 &&
            m.WeaponIds is { Length: <= 512 } && m.WeaponIds.All(id => id != Guid.Empty) && MasteryRules.ValidTree(m)) &&
        Masteries.Select(m => m.Key).Distinct().Count() == Masteries.Length &&
        Masteries.SelectMany(m => m.WeaponIds).Distinct().Count() == Masteries.Sum(m => m.WeaponIds.Length) &&
        Ammunition is { Length: <= 128 } && Ammunition.All(a => a != null && a.WeaponId != Guid.Empty &&
            a.ProjectileIds is { Length: > 0 and <= 16 } && a.ProjectileIds.All(id => id != Guid.Empty) && a.ProjectileIds.Distinct().Count() == a.ProjectileIds.Length) &&
        Ammunition.Select(a => a.WeaponId).Distinct().Count() == Ammunition.Length &&
        Ranges.All(r => r != null && r.Id != Guid.Empty && r.Name is { Length: > 0 and <= 80 } &&
            r.StartX is >= 0 and < 1024 && r.StartY is >= 0 and < 1024 && double.IsFinite(r.Wind) && Math.Abs(r.Wind) <= 3 && r.Rules?.IsValid == true &&
            (!r.Enabled || r.MapId != Guid.Empty)) && Ranges.Select(r => r.Id).Distinct().Count() == Ranges.Length;
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RoyalCombatConfiguration FromJson(string json)
    {
        if (json.Length > 262144) throw new InvalidDataException("royal-config-size");
        var result = JsonSerializer.Deserialize<RoyalCombatConfiguration>(json, new JsonSerializerOptions { MaxDepth = 12 });
        return result?.IsValid == true ? result : throw new InvalidDataException("royal-config-invalid");
    }
}
