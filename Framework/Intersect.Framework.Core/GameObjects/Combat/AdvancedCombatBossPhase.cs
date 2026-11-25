namespace Intersect.Framework.Core.GameObjects.Combat;

/// <summary>Optional enrage phase shared by the server and unit tests.</summary>
public static class AdvancedCombatBossPhase
{
    public static bool IsEnraged(bool isBoss, bool optIn, long health, long maximum,
        int thresholdPercent)
    {
        if (!isBoss || !optIn || maximum <= 0 || health <= 0)
            return false;
        var threshold = Math.Clamp(thresholdPercent, 5, 80);
        return health / (double)maximum * 100 <= threshold;
    }

    public static int WindupMilliseconds(int baseWindupMs, bool enraged,
        int reductionPercent)
    {
        var baseMs = Math.Clamp(baseWindupMs, 250, 2000);
        if (!enraged)
            return baseMs;
        var reduction = Math.Clamp(reductionPercent, 0, 50);
        return Math.Clamp((int)Math.Round(baseMs * (100 - reduction) / 100d), 250, 2000);
    }

    public static long Damage(long baseDamage, bool enraged, int bonusPercent)
    {
        if (!enraged || baseDamage <= 0)
            return baseDamage;
        var percent = Math.Clamp(bonusPercent, 0, 100);
        return (long)Math.Min(long.MaxValue, Math.Ceiling(baseDamage * (1 + percent / 100d)));
    }
}
