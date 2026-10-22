using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Client.Maps;

internal sealed class RainPuddleState
{
    private long _last;
    private float _coverage;

    internal float Update(Guid mapId, long utc, bool randomWeather, bool raining, int dryMinutes)
    {
        var dryMilliseconds = Math.Clamp(dryMinutes, 1, 30) * 60000L;
        if (randomWeather)
        {
            // Recover the exact storm age after changing maps or reconnecting, including the previous cycle.
            var current = MapWeatherCycle.Window(mapId, utc);
            if (raining && utc >= current.Start && utc < current.End)
                return Math.Clamp((utc - current.Start) / 60000f, 0, 1);
            if (raining) return 0;
            var previous = MapWeatherCycle.Window(mapId, Math.Max(0, utc - MapWeatherCycle.CycleMilliseconds));
            var ended = current.End <= utc ? current.End : previous.End;
            return ended > utc ? 0 : Math.Clamp(1 - (utc - ended) / (float)dryMilliseconds, 0, 1);
        }
        var elapsed = _last == 0 || utc < _last ? 0 : utc - _last;
        _last = utc;
        _coverage = Math.Clamp(_coverage + (raining ? elapsed / 60000f : -elapsed / (float)dryMilliseconds), 0, 1);
        return _coverage;
    }
}
