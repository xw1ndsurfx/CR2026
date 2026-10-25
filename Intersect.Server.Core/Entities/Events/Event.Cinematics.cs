using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Server;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities.Events;

public partial class Event
{
    internal bool CinematicActive { get; private set; }
    private Guid _cinematicMap;
    private Guid _cinematicInstance;
    private long _cinematicDeadline;
    private bool _cinematicWasHolding;
    private EventPageInstance? _cinematicOwnerPage;
    private readonly List<(EventPageInstance Page, Action Restore)> _cinematicActors = [];

    internal bool TryBeginCinematic(BeginCinematicCommand command)
    {
        if (CinematicActive || command.MapId != Player.MapId || command.Actors.Count > 64 ||
            Player.EventLookup.Any(p => p.Value != this && (p.Value.CinematicActive || p.Value.HoldingPlayer)) ||
            Player.MoveRoute is { Complete: false }) return false;
        var actors = new List<EventPageInstance>();
        foreach (var actor in command.Actors)
        {
            if (actor.TargetId == Guid.Empty)
            {
                if (Player.X != actor.StartX || Player.Y != actor.StartY) return false;
                continue;
            }
            var evt = Player.EventLookup.Values.FirstOrDefault(e => e.Descriptor.Id == actor.TargetId);
            if (evt == null || evt.Global || evt.MapId != command.MapId || evt.PageInstance is not { } page ||
                page.GlobalClone != null || page.X != actor.StartX || page.Y != actor.StartY ||
                page.MoveRoute is { Complete: false, Actions.Count: > 0 }) return false;
            actors.Add(page);
        }
        foreach (var actor in actors) _cinematicActors.Add((actor, actor.CaptureCinematicMovement()));
        _cinematicWasHolding = HoldingPlayer;
        _cinematicOwnerPage = PageInstance;
        CinematicActive = true; _cinematicMap = Player.MapId; _cinematicInstance = Player.MapInstanceId;
        _cinematicDeadline = Environment.TickCount64 + 30 * 60 * 1000;
        return true;
    }
    internal void FinishCinematic(bool interrupted = false)
    {
        if (!CinematicActive) return;
        foreach (var actor in _cinematicActors)
        {
            actor.Restore();
            if (Player.MapId == _cinematicMap && Player.MapInstanceId == _cinematicInstance &&
                Player.EventLookup.Values.Any(evt => evt.PageInstance == actor.Page))
                PacketSender.SendEntityDataTo(Player, actor.Page);
        }
        _cinematicActors.Clear();
        if (_cinematicOwnerPage != null && Player.MoveRouteSetter == _cinematicOwnerPage)
        {
            Player.MoveRoute = null;
            Player.MoveRouteSetter = null;
            PacketSender.SendMoveRouteToggle(Player, false);
        }
        CinematicActive = false;
        _cinematicOwnerPage = null;
        Player.SendPacket(new CinematicCameraPacket { EventId = Descriptor.Id, Reset = true });
        var restoreHold = _cinematicWasHolding && !interrupted;
        if (restoreHold && !HoldingPlayer) PacketSender.SendHoldPlayer(Player, Descriptor.Id, Descriptor.MapId);
        if (!restoreHold && HoldingPlayer) PacketSender.SendReleasePlayer(Player, Descriptor.Id);
        HoldingPlayer = restoreHold;
    }
    private void CheckCinematicLifetime()
    {
        if (!CinematicActive) return;
        if (CallStack.Count == 0 || Player.MapId != _cinematicMap || Player.MapInstanceId != _cinematicInstance ||
            Environment.TickCount64 > _cinematicDeadline || PlayerHasDied)
        { CallStack.Clear(); FinishCinematic(interrupted: true); }
    }
}

public partial class EventPageInstance
{
    internal Action CaptureCinematicMovement()
    {
        var movement = MovementType; var speed = MovementSpeed; var actualSpeed = Speed; var freq = MovementFreq; var directionFix = mDirectionFix;
        var saved = new EventMoveRoute(); saved.CopyFrom(MoveRoute); saved.Complete = MoveRoute.Complete; saved.ActionIndex = MoveRoute.ActionIndex;
        // Reserve personal actors immediately, including gaps before their first route.
        // Otherwise a random-movement page can wander away from its validated anchor.
        MovementType = EventMovementType.None;
        return () =>
        {
            MoveRoute.CopyFrom(saved); MoveRoute.Complete = saved.Complete; MoveRoute.ActionIndex = saved.ActionIndex;
            MovementType = movement; MovementFreq = freq;
            MovementSpeed = speed; Speed = actualSpeed; mDirectionFix = directionFix;
        };
    }
}
