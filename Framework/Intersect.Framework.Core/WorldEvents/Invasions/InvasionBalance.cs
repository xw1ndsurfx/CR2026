using Intersect.Enums;

namespace Intersect.Framework.Core.WorldEvents.Invasions;

/// <summary>Instance-only horde limits, based on the Auto NPC Creator's normal melee preset.</summary>
public static class InvasionBalance
{
    public static int AverageLevel(IEnumerable<int> levels) =>
        Math.Max(1, (int)Math.Round(levels.Where(level => level > 0).DefaultIfEmpty(1).Average(), MidpointRounding.AwayFromZero));

    // Large waves need weaker individuals. Population increases stay bounded even on busy servers.
    public static double PopulationHealth(int count, int extraPercent) =>
        Math.Min(2d, 1d + (Math.Sqrt(Math.Max(1, count)) - 1d) * Math.Min(0.25d, Math.Max(0, extraPercent) / 100d));
    public static double PopulationDamage(int count) => Math.Min(1.25d, 1d + (Math.Sqrt(Math.Max(1, count)) - 1d) * 0.05d);
    public static double HealthLimit(int level) => (30d + 12d * level + 1.5d * level * level) * 0.5d;
    public static double DamageLimit(int level) => 3d + 1.8d * level;
    public static double StatLimit(int level, Stat stat) => stat switch
    {
        Stat.Attack or Stat.AbilityPower => (3d + 2.2d * level) * 0.65d,
        Stat.Defense or Stat.MagicResist => (2d + 1.1d * level) * 0.65d,
        _ => 2d + 0.8d * level,
    };
}
