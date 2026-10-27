using Intersect.Framework.Core.Professions;

namespace Intersect.Framework.Core.Jousting;

/// <summary>Profession view of the existing saved jousting career; no second XP store.</summary>
public static class JoustProfession
{
    public static readonly Guid Id = Guid.Parse("f0c40000-0000-4000-8000-000000000001");
    public const int MaximumLevel = 25;
    public static bool IsNative(Guid id) => id == Id;
    public static (int Level, long Experience, long Required) Progress(long total)
    {
        total = Math.Clamp(total, 0, 1_000_000_000);
        var level = (int)Math.Min(MaximumLevel, 1 + Math.Sqrt(total / 100d));
        if (level >= MaximumLevel) return (level, 0, 0);
        var floor = 100L * (level - 1) * (level - 1);
        return (level, total - floor, 100L * level * level - floor);
    }
    public static void Include(ProfessionConfiguration configuration) =>
        configuration.Professions = configuration.Professions.Where(p => !IsNative(p.Id)).Append(new ProfessionDefinition
        {
            Id = Id, Name = "Jousting", Description = "Jousting progression", MaximumLevel = MaximumLevel,
        }).ToArray();
}
