namespace Intersect.Client.Naval;

/// <summary>A snapshot refresh must not restart a one-shot animation.</summary>
internal sealed class ShipAnimationClock
{
    private string? _graphics;
    private long _started;

    internal void Observe(string graphics, long now)
    {
        if (string.Equals(_graphics, graphics, StringComparison.OrdinalIgnoreCase)) return;
        _graphics = graphics; _started = now;
    }

    internal long Elapsed(long now) => Math.Max(0, now - _started);
}
