namespace Intersect.Client.Maps;

// A monotonic clock makes transitions independent of frame rate and server UTC corrections.
internal sealed class WeatherTransition
{
    internal const long DurationMilliseconds = 10000;
    private long? _updated;
    internal float Amount { get; private set; }
    internal float Update(bool active, long now)
    {
        var elapsed = _updated is { } previous ? Math.Max(0, now - previous) : 0;
        _updated = now;
        var step = Math.Min(1f, elapsed / (float)DurationMilliseconds);
        Amount = active ? Math.Min(1f, Amount + step) : Math.Max(0f, Amount - step);
        return Amount;
    }
    internal void Reset() { Amount = 0; _updated = null; }
    internal void CopyFrom(WeatherTransition source) { Amount = source.Amount; _updated = source._updated; }

    internal static float Approach(float current, float target, float maximumStep) =>
        current < target ? Math.Min(target, current + maximumStep) : Math.Max(target, current - maximumStep);
}
