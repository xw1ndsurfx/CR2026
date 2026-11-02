using System.ComponentModel;

namespace Intersect.Framework.Core.Archery;

/// <summary>A weapon technique, independent of learned spells and spellbook slots.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class WeaponSkillDefinition
{
    public bool Enabled { get; set; }
    public int Range { get; set; } = 1;
    public int CooldownMilliseconds { get; set; } = 3000;
    public int DamagePercent { get; set; } = 100;
    public int DamagePercentPerRank { get; set; } = 15;
    [Browsable(false)] public Guid AnimationId { get; set; }
    [Browsable(false)] public Guid HitAnimationId { get; set; }
    public int Power(int rank) => DamagePercent + Math.Clamp(rank-1,0,4)*DamagePercentPerRank;
    public bool IsValid => Range is >=1 and <=12 && CooldownMilliseconds is >=500 and <=120000 &&
        DamagePercent is >=1 and <=500 && DamagePercentPerRank is >=0 and <=100;
}
