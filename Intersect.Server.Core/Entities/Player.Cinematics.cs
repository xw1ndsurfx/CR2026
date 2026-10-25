using GameEvent = Intersect.Server.Entities.Events.Event;

namespace Intersect.Server.Entities;

public partial class Player
{
    private sealed record CinematicRouteScope(GameEvent Owner, int ThreadId);
    private CinematicRouteScope? _cinematicRouteScope;

    private bool HasBlockingEventHold => EventLookup.Values.Any(evt =>
        evt.HoldingPlayer && !CanIgnoreOwnCinematicHold(evt));

    private bool CanIgnoreOwnCinematicHold(GameEvent evt)
    {
        var scope = _cinematicRouteScope;
        return scope != null && scope.ThreadId == Environment.CurrentManagedThreadId &&
            ReferenceEquals(scope.Owner, evt) && evt.OwnsActiveCinematicRoute(this, MoveRouteSetter);
    }

    protected override bool ProcessMoveRoute(Player forPlayer, long timeMs)
    {
        var owner = MoveRouteSetter?.MyEventIndex;
        // A stale scene route must not move after a warp or a page replacement,
        // even when an interactive clip has temporarily released its input hold.
        if (owner is { CinematicActive: true } && !owner.OwnsActiveCinematicRoute(this, MoveRouteSetter))
            return true;

        var previous = _cinematicRouteScope;
        _cinematicRouteScope = owner is { CinematicActive: true }
            ? new(owner, Environment.CurrentManagedThreadId)
            : null;
        try
        {
            // Only this native interpreter call may ignore its own scene's hold.
            // Concurrent packet handlers, other Events, terrain and death checks retain their gates.
            return base.ProcessMoveRoute(forPlayer, timeMs);
        }
        finally
        {
            _cinematicRouteScope = previous;
        }
    }
}
