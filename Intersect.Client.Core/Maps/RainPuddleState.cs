using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Client.Maps;

internal sealed class RainPuddleState
{
    private long _last;
    private float _coverage;

    internal float Update(Guid mapId, long utc, bool randomWeather, bool raining, int dryMinutes, long forcedStarted = 0, long forcedUntil = 0)
    {
        var dryMilliseconds = Math.Clamp(dryMinutes, 1, 30) * 60000L;
        if (randomWeather)
        {
            // Recover the exact storm age after changing maps or reconnecting, including the previous cycle.
            var current = MapWeatherCycle.Window(mapId, utc);
            if (raining)
            {
                var start = forcedUntil > utc && forcedStarted > 0 ? forcedStarted : current.Start;
                if (utc >= current.Start && utc < current.End) start = Math.Min(start,current.Start);
                _last = utc;
                return _coverage = Math.Clamp((utc-start)/60000f,0,1);
            }
            var previous = MapWeatherCycle.Window(mapId, Math.Max(0, utc - MapWeatherCycle.CycleMilliseconds));
            var ended = current.End <= utc ? current.End : previous.End;
            if(forcedUntil > 0 && forcedUntil <= utc) ended = Math.Max(ended,forcedUntil);
            var peak = ended == forcedUntil ? Math.Clamp((forcedUntil-forcedStarted)/60000f,0,1) : 1;
            _last=utc;
            return _coverage = ended > utc ? 0 : Math.Clamp(peak - (utc-ended)/(float)dryMilliseconds,0,1);
        }
        var elapsed = _last == 0 || utc < _last ? 0 : utc - _last;
        _last = utc;
        _coverage = Math.Clamp(_coverage + (raining ? elapsed / 60000f : -elapsed / (float)dryMilliseconds), 0, 1);
        return _coverage;
    }
}
