using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities.Events;

public partial class Event
{
    private sealed class TemporaryActor(Event evt, bool visible)
    { internal Event Event = evt; internal bool Visible = visible; }
    private readonly Dictionary<Guid, TemporaryActor> _cinematicTemporaryActors = [];

    private bool CanCreateTemporaryActor(CinematicActor actor, Guid mapId) =>
        actor.TargetId != Guid.Empty && actor.StartX >= 0 && actor.StartY >= 0 &&
        actor.StartX < Options.Instance.Map.MapWidth && actor.StartY < Options.Instance.Map.MapHeight &&
        NPCDescriptor.Get(actor.NPCId) is { Sprite.Length: > 0 } &&
        !EventDescriptor.TryGet(actor.TargetId, out _) &&
        !Player.EventLookup.Values.Any(e => e.Descriptor.Id == actor.TargetId) &&
        Intersect.Server.Maps.MapController.Get(mapId) is { } map && map.Attributes[actor.StartX, actor.StartY] is not MapBlockedAttribute;

    private bool CreateTemporaryActor(CinematicActor source)
    {
        var map = Intersect.Server.Maps.MapController.Get(_cinematicMap);
        if (map == null || NPCDescriptor.Get(source.NPCId) is not { } npc) return false;
        var page = new EventPage { Passable = true, HideName = source.HideName, DisablePreview = true,
            WalkingAnimation = true, Graphic = new EventGraphic { Type = EventGraphicType.Sprite, Filename = npc.Sprite, X = 0, Y = 0 } };
        page.Movement.Type = EventMovementType.None;
        var descriptor = new EventDescriptor(source.TargetId, _cinematicMap, source.StartX, source.StartY)
            { Name = source.Name, Pages = [page], Global = false, CommonEvent = false };
        var evt = new Event(Guid.NewGuid(), map, Player, descriptor);
        if (!Player.EventLookup.TryAdd(evt.Id, evt)) return false;
        _cinematicTemporaryActors.Add(source.TargetId, new(evt, source.VisibleAtStart));
        evt.PageInstance = new EventPageInstance(descriptor, page, _cinematicMap, _cinematicInstance, evt, Player, sendInitial: false);
        if (source.VisibleAtStart) evt.PageInstance.SendToPlayer();
        return true;
    }

    internal void SetCinematicActorVisibility(Guid actor, bool visible)
    {
        if (CinematicActive && _cinematicVessels.TryGetValue(actor,out var vessel))
        { vessel.Visible=visible;PublishCinematicFleet(true);return; }
        if (!CinematicActive || Player.MapId != _cinematicMap || Player.MapInstanceId != _cinematicInstance ||
            !_cinematicTemporaryActors.TryGetValue(actor, out var entry) || entry.Visible == visible ||
            entry.Event.PageInstance is not { } page) return;
        entry.Visible = visible;
        if (visible) page.SendToPlayer(); else PacketSender.SendEntityLeaveTo(Player, page);
    }

    private void UpdateCinematicTemporaryActors(long timeMs)
    {
        if (!CinematicActive) return;
        foreach (var actor in _cinematicTemporaryActors.Values) actor.Event.PageInstance?.Update(false, timeMs);
    }

    private void RemoveCinematicTemporaryActors()
    {
        foreach (var actor in _cinematicTemporaryActors.Values)
        {
            Player.EventLookup.TryRemove(actor.Event.Id, out _);
            // These actors were never inserted in the map's tile/base-descriptor indices.
            // Do not remove an unrelated map Event sharing their starting tile.
            if (actor.Event.PageInstance is { } page)
            { PacketSender.SendEntityLeaveTo(Player, page); page.Dispose(); }
        }
        _cinematicTemporaryActors.Clear();
    }
}
