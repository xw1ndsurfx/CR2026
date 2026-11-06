namespace Intersect.Framework.Core.GameObjects.Combat;

/// <summary>
/// A server-side, per-player PvE melee combo. Only successful health damage
/// progresses it. No client input or visual timer can advance the sequence.
/// </summary>
public sealed class AdvancedPlayerCombo
{
    public const int MaximumSteps = 3;
    private Guid _targetId;
    private Guid _mapId;
    private Guid _instanceId;
    private long _lastHitTime = -1;

    public int CompletedHits { get; private set; }

    public bool IsOnMap(Guid mapId, Guid instanceId) =>
        CompletedHits == 0 || _mapId == mapId && _instanceId == instanceId;

    public static int ClampWindow(int windowMs) => Math.Clamp(windowMs, 600, 6000);

    public bool IsActive(long nowMs, int windowMs) =>
        CompletedHits > 0 && nowMs >= _lastHitTime &&
        nowMs - _lastHitTime < ClampWindow(windowMs);

    public int RemainingMilliseconds(long nowMs, int windowMs) =>
        IsActive(nowMs, windowMs)
            ? (int)Math.Max(0L, Math.Min(6000L, _lastHitTime + ClampWindow(windowMs) - nowMs))
            : 0;

    public int NextHitStep(
        long nowMs, Guid targetId, Guid mapId, Guid instanceId, int windowMs)
    {
        if (targetId == Guid.Empty || mapId == Guid.Empty)
            return 1;
        return IsActive(nowMs, windowMs) &&
               _targetId == targetId && _mapId == mapId && _instanceId == instanceId
            ? Math.Clamp(CompletedHits + 1, 1, MaximumSteps)
            : 1;
    }

    /// <summary>
    /// Record one real NPC melee hit. Returns 1, 2 or 3. A completed third
    /// attack resets the chain, so spamming attacks cannot extend a combo.
    /// </summary>
    public int RegisterSuccessfulHit(
        long nowMs, Guid targetId, Guid mapId, Guid instanceId, int windowMs)
    {
        if (targetId == Guid.Empty || mapId == Guid.Empty)
        {
            Reset();
            return 0;
        }

        var step = NextHitStep(nowMs, targetId, mapId, instanceId, windowMs);
        if (step >= MaximumSteps)
        {
            Reset();
            return MaximumSteps;
        }

        CompletedHits = step;
        _targetId = targetId;
        _mapId = mapId;
        _instanceId = instanceId;
        _lastHitTime = nowMs;
        return step;
    }

    public bool Expire(long nowMs, int windowMs)
    {
        if (CompletedHits == 0 || IsActive(nowMs, windowMs))
            return false;
        Reset();
        return true;
    }

    public void Reset()
    {
        CompletedHits = 0;
        _targetId = Guid.Empty;
        _mapId = Guid.Empty;
        _instanceId = Guid.Empty;
        _lastHitTime = -1;
    }

    public static int ApplyBonus(long damage, int percent)
    {
        if (damage <= 0)
            return 0;
        var boosted = (decimal)damage *
            (100 + Math.Clamp(percent, 0, 100)) / 100m;
        return (int)Math.Min(int.MaxValue, decimal.Ceiling(boosted));
    }

    public static long ApplyBonusLong(long damage, int percent)
    {
        if (damage <= 0)
            return damage;
        var boosted = (decimal)damage *
            (100 + Math.Clamp(percent, 0, 100)) / 100m;
        return (long)Math.Min(long.MaxValue, decimal.Ceiling(boosted));
    }
}
