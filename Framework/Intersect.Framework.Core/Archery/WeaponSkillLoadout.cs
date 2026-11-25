namespace Intersect.Framework.Core.Archery;

public static class WeaponSkillLoadout
{
    public const int SlotCount = 10;
    public static bool Available(MasteryCategory category, MasteryTalent talent, long experience,
        IReadOnlyDictionary<string, int> ranks) => talent.Skill.Enabled && ranks.GetValueOrDefault(talent.Key) > 0 &&
        MasteryRules.Eligible(category, talent, experience, ranks);

    public static bool Valid(string[]? slots) => slots is { Length: SlotCount } &&
        slots.All(key => key != null && (key.Length == 0 || MasteryRules.Key(key))) &&
        slots.Where(key => key.Length > 0).Distinct().Count() == slots.Count(key => key.Length > 0);

    public static string[] Resolve(MasteryCategory category, long experience, IReadOnlyDictionary<string, int> ranks, string[]? saved)
    {
        var available = category.Talents.Where(t => Available(category, t, experience, ranks)).Select(t => t.Key).ToArray();
        var source = saved ?? available;
        var result = Enumerable.Repeat("", SlotCount).ToArray();
        for (var slot = 0; slot < Math.Min(source.Length, SlotCount); slot++)
            if (available.Contains(source[slot]) && !result.Contains(source[slot])) result[slot] = source[slot];
        return result;
    }

    public static string[] Assign(string[] current, int slot, string talent)
    {
        if (!Valid(current) || slot is < 0 or >= SlotCount || talent == null || talent.Length > 0 && !MasteryRules.Key(talent))
            throw new ArgumentException("weapon-skill-slot");
        var result = (string[])current.Clone();
        if (talent.Length > 0)
            for (var index = 0; index < result.Length; index++) if (result[index] == talent) result[index] = "";
        result[slot] = talent;
        return result;
    }
}
