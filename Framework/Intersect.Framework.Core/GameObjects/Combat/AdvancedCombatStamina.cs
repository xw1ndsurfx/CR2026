namespace Intersect.Framework.Core.GameObjects.Combat;

/// <summary>
/// Deterministic stamina with fractional regeneration and a delay after spending.
/// All timing and costs are authorized by the game server, not client input.
/// </summary>
public sealed class AdvancedCombatStamina
{
    public const int MaximumCap = 500;
    private long _lastTick = -1;
    private long _lastSpend = long.MinValue / 2;
    private double _fraction;

    public int Maximum { get; private set; } = 100;
    public int Current { get; private set; } = 100;

    public void Advance(long nowMs, int maximum, int regenPerSecond, int delayMs)
    {
        maximum = Math.Clamp(maximum, 1, MaximumCap);
        regenPerSecond = Math.Clamp(regenPerSecond, 0, 100);
        delayMs = Math.Clamp(delayMs, 0, 10_000);

        if (_lastTick < 0)
        {
            Maximum = maximum;
            Current = maximum;
            _lastTick = nowMs;
            return;
        }

        Maximum = maximum;
        Current = Math.Clamp(Current, 0, maximum);
        if (nowMs <= _lastTick)
            return;

        var fromMs = Math.Max(_lastTick, _lastSpend + delayMs);
        if (nowMs > fromMs && Current < maximum)
        {
            _fraction += (nowMs - fromMs) * (regenPerSecond / 1000.0);
            var gain = (int)_fraction;
            if (gain > 0)
            {
                Current = Math.Min(maximum, Current + gain);
                _fraction -= gain;
            }
            if (Current >= maximum)
                _fraction = 0;
        }
        _lastTick = nowMs;
    }

    public bool CanSpend(int cost)
    {
        cost = Math.Clamp(cost, 0, Maximum);
        return Current >= cost;
    }

    public bool TrySpend(int cost, long nowMs)
    {
        cost = Math.Clamp(cost, 0, Maximum);
        if (Current < cost)
            return false;

        Current -= cost;
        if (cost > 0)
        {
            _lastSpend = nowMs;
            _fraction = 0;
        }
        return true;
    }
}
