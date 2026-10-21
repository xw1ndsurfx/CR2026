namespace Intersect.Client.Maps;

/// <summary>Bounded, world-anchored rain simulation independent of textures and frame rate.</summary>
internal sealed class GeneratedRainSimulation
{
    internal sealed class Drop
    {
        internal float X, Y, LandingY, Depth;
    }

    private readonly List<Drop> _drops = [];
    private readonly Random _random;
    internal IReadOnlyList<Drop> Drops => _drops;
    internal const int MaximumDrops = 512;

    internal GeneratedRainSimulation(Random? random = null) => _random = random ?? new Random();
    internal void Clear() => _drops.Clear();
    internal static float FallSpeed(int speed) => speed == 0 ? 800 : Math.Clamp(Math.Abs((long)speed), 1, 5) * 220;
    internal static float WindSpeed(int speed) => Math.Clamp(speed, -5, 5) * 100;

    internal void Update(float left, float top, float width, float height, int intensity,
        int wind, int fallSpeed, float elapsedSeconds, Action<float, float>? impact)
    {
        var amount = Math.Clamp(intensity, 0, 100);
        if (amount == 0 || width <= 0 || height <= 0)
        {
            Clear();
            return;
        }

        var count = (int)Math.Clamp(width * (double)height / 5000 * amount / 100, 1, MaximumDrops);
        var dx = WindSpeed(wind);
        var dy = FallSpeed(fallSpeed);
        var elapsed = float.IsFinite(elapsedSeconds) ? Math.Clamp(elapsedSeconds, 0, .1f) : 0;
        if (_drops.Count > count) _drops.RemoveRange(count, _drops.Count - count);
        while (_drops.Count < count)
        {
            var drop = new Drop();
            Reset(drop, true);
            _drops.Add(drop);
        }

        foreach (var drop in _drops)
        {
            var oldY = drop.Y;
            var step = elapsed * drop.Depth;
            drop.X += dx * step;
            drop.Y += dy * step;
            if (drop.Y >= drop.LandingY)
            {
                // Interpolate the impact so a long frame cannot move it past the landing point.
                var overshoot = (drop.Y - drop.LandingY) / dy;
                var x = drop.X - dx * overshoot;
                if (oldY < drop.LandingY && x >= left && x <= left + width &&
                    drop.LandingY >= top && drop.LandingY <= top + height)
                    impact?.Invoke(x, drop.LandingY);
                Reset(drop, false);
            }
            else if (drop.X < left - 100 || drop.X > left + width + 100 ||
                     drop.Y < top - 100 || drop.Y > top + height + 100)
            {
                Reset(drop, true);
            }
        }

        void Reset(Drop drop, bool scatter)
        {
            drop.Depth = .65f + (float)_random.NextDouble() * .7f;
            drop.LandingY = top + height * (.12f + (float)_random.NextDouble() * .88f);
            var start = top - 24;
            // Pick an on-screen landing point, then work backwards against the wind.
            drop.X = left + (float)_random.NextDouble() * width - dx * (drop.LandingY - start) / dy;
            drop.Y = start;
            if (!scatter) return;
            var phase = (float)_random.NextDouble();
            drop.X += dx * (drop.LandingY - start) / dy * phase;
            drop.Y += (drop.LandingY - start) * phase;
        }
    }
}
