using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Server;
using Intersect.Server.Networking;
using Intersect.Framework.Core.Cinematics;
namespace Intersect.Server.Entities.Events;

public partial class Event
{
    internal bool CinematicActive { get; private set; }
    internal long CinematicRunVersion { get; private set; }
    internal long CinematicFinishedVersion { get; private set; }
    internal bool CinematicFinishedSuccessfully { get; private set; }
    private Guid _cinematicMap;
    private Guid _cinematicInstance;
    private long _cinematicDeadline;
    private bool _cinematicWasHolding;
    private EventPageInstance? _cinematicOwnerPage;
    private readonly List<(EventPageInstance Page, Action Restore)> _cinematicActors = [];

    internal bool OwnsActiveCinematicRoute(Player player, EventPageInstance? page) =>
        CinematicActive && ReferenceEquals(Player, player) &&
        player.MapId == _cinematicMap && player.MapInstanceId == _cinematicInstance &&
        page != null && ReferenceEquals(_cinematicOwnerPage, page) &&
        ReferenceEquals(PageInstance, page) && ReferenceEquals(page.MyEventIndex, this);

    internal bool IsPlayingCinematic(Guid sceneId) => CinematicActive && (sceneId == Guid.Empty ||
        EventDescriptor.TryGet(sceneId, out var scene) && CallStack.Any(frame => scene.Pages.Contains(frame.Page)));

    internal bool TryBeginCinematic(BeginCinematicCommand command)
    {
        if (CinematicActive || command.MapId != Player.MapId || command.Actors == null || command.Actors.Count > 64 ||
            command.Actors.Any(a => a == null) || command.Actors.Select(a => a.TargetId).Distinct().Count() != command.Actors.Count ||
            Player.EventLookup.Any(p => p.Value != this && (p.Value.CinematicActive || p.Value.HoldingPlayer)) ||
            Player.MoveRoute is { Complete: false }) return false;
        var actors = new List<EventPageInstance>();
        foreach (var actor in command.Actors)
        {
            if (!Enum.IsDefined(actor.Kind)) return false;
            if (actor.Kind == CinematicActorKind.TemporaryVessel) continue;
            if (actor.Kind == CinematicActorKind.TemporaryNpc)
            { if (!CanCreateTemporaryActor(actor, command.MapId)) return false; continue; }
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
        CinematicRunVersion++;
        _allowCinematicVisualSkip = command.AllowVisualSkip; _cinematicSkipping = false;
        CinematicActive = true; _cinematicMap = Player.MapId; _cinematicInstance = Player.MapInstanceId;
        _cinematicDeadline = Environment.TickCount64 + 30 * 60 * 1000;
        try
        {
            if (!CreateCinematicVessels(command.Actors)) { FinishCinematic(interrupted: true); return false; }
            foreach (var actor in command.Actors.Where(a => a.Kind == CinematicActorKind.TemporaryNpc))
                if (!CreateTemporaryActor(actor)) { FinishCinematic(interrupted: true); return false; }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { FinishCinematic(interrupted: true); return false; }
        SendCinematicControl(true);
        return true;
    }
    internal void FinishCinematic(bool interrupted = false)
    {
        if (!CinematicActive) return;
        CinematicFinishedVersion = CinematicRunVersion;
        CinematicFinishedSuccessfully = !interrupted;
        foreach (var actor in _cinematicActors)
        {
            actor.Restore();
            if (Player.MapId == _cinematicMap && Player.MapInstanceId == _cinematicInstance &&
                Player.EventLookup.Values.Any(evt => evt.PageInstance == actor.Page))
                PacketSender.SendEntityDataTo(Player, actor.Page);
        }
        _cinematicActors.Clear();
        RemoveCinematicTemporaryActors();
        RemoveCinematicVessels();
        if (_cinematicOwnerPage != null && Player.MoveRouteSetter == _cinematicOwnerPage)
        {
            Player.MoveRoute = null;
            Player.MoveRouteSetter = null;
            PacketSender.SendMoveRouteToggle(Player, false);
        }
        SendCinematicControl(false);
        CinematicActive = false;
        _allowCinematicVisualSkip = false; _cinematicSkipping = false;
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
            Environment.TickCount64 > _cinematicDeadline || PlayerHasDied || _cinematicTemporaryActors.Values.Any(a =>
                a.Event.PageInstance == null || !Player.EventLookup.TryGetValue(a.Event.Id, out var current) || !ReferenceEquals(current, a.Event)))
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
