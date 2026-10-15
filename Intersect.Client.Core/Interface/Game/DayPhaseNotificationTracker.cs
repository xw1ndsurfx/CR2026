using Intersect.GameObjects;

namespace Intersect.Client.Interface.Game;

internal sealed class DayPhaseNotificationTracker
{
    private DayPhase? _previous;
    internal bool Observe(DayPhase phase)
    {
        var changed = _previous.HasValue && _previous != phase;
        _previous = phase;
        return changed;
    }
}
