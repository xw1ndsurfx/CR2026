using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

/// <summary>Opt-in overworld prototype. All mutation happens under the captain's entity lock then Gate.</summary>
internal static partial class NavalSystem
{
    private sealed class Vessel(ShipDefinition definition)
    {
        internal readonly ShipDefinition Definition = definition;
        internal readonly ShipState State = new() { Id = definition.Id, MapId = definition.MapId,
            X = definition.X, Y = definition.Y, Heading = ShipMovement.Normalize(definition.Heading) };
        internal readonly ShipInputAuthority Controls = new();
        internal long LastStep;
        internal Guid OwnerId;
        internal string SinkingGraphics = "";
        internal long SunkUntil;
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Vessel> Ships = [];
    private static readonly NavalConfigurationStore Configuration = new(Path.Combine("resources", "naval.json"));
    private static bool _loaded, _enabled;
    private static bool IsEnabled()
    {
        try
        {
            if (!_loaded) ApplyConfiguration(Configuration.Load());
            return _enabled;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return false; }
    }

    private static void ApplyConfiguration(NavalConfiguration settings)
    {
        // Adding a definition never resets the position or ownership of an existing vessel.
        foreach (var definition in settings.Ships)
            if (!Ships.ContainsKey(definition.Id)) Ships.Add(definition.Id, new Vessel(definition));
        _enabled = settings.Enabled; _loaded = true;
    }

    internal static NavalSetupResultPacket PrepareMap(Guid mapId)
    {
        lock (Gate)
        {
            var map = MapController.Get(mapId);
            if (map == null || !OceanMapResolver.Maps().Contains(mapId))
                return new() { Outcome = NavalSetupOutcome.NotOcean };
            try
            {
                _ = IsEnabled();
                var prepared = Configuration.Prepare(mapId, configured => FindPlacement(map, configured));
                ApplyConfiguration(prepared.Configuration);
                var ship = prepared.Ship;
                return new() { Outcome = prepared.Created ? NavalSetupOutcome.Created : NavalSetupOutcome.Existing,
                    MapName = map.Name, ShipId = ship.Id, X = ship.X, Y = ship.Y, DockX = ship.DockX, DockY = ship.DockY };
            }
            catch (NoShipPlacementException) { return new() { Outcome = NavalSetupOutcome.NoSpace }; }
            catch (ShipLimitException) { return new() { Outcome = NavalSetupOutcome.ShipLimit }; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { return new() { Outcome = NavalSetupOutcome.ConfigurationError }; }
        }
    }

    private static ShipDefinition FindPlacement(MapController map, IReadOnlyList<ShipDefinition> configured)
    {
        var blocked = map.GetEntitiesOnAllInstances().Where(e => e.MapInstanceId == MapInstance.OverworldInstanceId && e.Z == 0 && !e.IsPassable())
            .Select(e => (e.X, e.Y)).ToHashSet();
        foreach (var evt in EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => !e.CommonEvent && e.MapId == map.Id && e.Pages.Any(p => !p.Passable)))
            blocked.Add((evt.SpawnX, evt.SpawnY));
        foreach (var spawn in map.Spawns.Where(s => s.X >= 0 && s.Y >= 0)) blocked.Add((spawn.X, spawn.Y));
        if (map.TryGetInstance(MapInstance.OverworldInstanceId, out var instance))
            foreach (var page in instance.GlobalEventInstances.Values.SelectMany(e => e.GlobalPageInstance).Where(p => p != null && !p.Passable && p.Z == 0))
                blocked.Add((page.X, page.Y));
        var ocean = OceanMapResolver.Maps();
        foreach (var vessel in Ships.Values)
        {
            foreach (var tile in HullTiles(vessel, ocean, vessel.State.MapId, vessel.State.X, vessel.State.Y))
                if (tile.Map == map.Id) blocked.Add((tile.X, tile.Y));
            if (vessel.Definition.MapId == map.Id) blocked.Add((vessel.Definition.DockX, vessel.Definition.DockY));
        }
        foreach (var ship in configured.Where(d => !Ships.ContainsKey(d.Id) && d.MapId == map.Id))
        {
            blocked.UnionWith(ShipFootprint.Tiles(ship.Width, ship.Length, ship.X, ship.Y));
            blocked.Add((ship.DockX, ship.DockY));
        }
        return ShipPlacement.Find(map.Id, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight,
            (x, y) => map.Attributes == null || x >= map.Attributes.GetLength(0) || y >= map.Attributes.GetLength(1) ||
                blocked.Contains((x, y)) || IsBlockingAttribute(map.Attributes[x, y]));
    }

    internal static void Helm(Player player)
    {
        lock (player.EntityLock)
        {
            if (player.CaptainedShipId == Guid.Empty &&
                ShipDock.Nearest(MapController.Get(player.MapId)?.Attributes, player.X + .5, player.Y + .5, 1.5) != null)
            { Harbour(player, new() { Action = HarbourAction.Open }); return; }
            ShipState anchor = null;
            (Guid MapId, int X, int Y)? dock = null;
            lock (Gate)
            {
                if (!IsEnabled() || player.InstanceType != MapInstanceType.Overworld || !player.InGame ||
                    player.IsDead || player.HasPendingDeath || player.Client == null || player.NavalBusy || player.Z != 0 ||
                    player.MoveRoute != null)
                { Notice(player, "Naval navigation is unavailable."); return; }
                var ocean = OceanMapResolver.Maps();
                if (player.CaptainedShipId != Guid.Empty)
                {
                    if (!Ships.TryGetValue(player.CaptainedShipId, out var active)) return;
                    var d = active.Definition; var s = active.State;
                    if (Math.Abs(s.Speed) > .1)
                    { Notice(player, "Anchor with Space and stop the ship before disembarking."); return; }
                    if (active.SunkUntil > 0) { Notice(player, "Your ship has sunk. Return to the harbour to repair it."); return; }
                    var attributes = MapController.Get(s.MapId)?.Attributes;
                    bool ExitClear(int x, int y) => ClearTile(player, s.MapId, x, y) &&
                        !HullTiles(active, ocean, s.MapId, s.X, s.Y).Contains((s.MapId, x, y)) &&
                        !Ships.Values.Any(other => other != active && HullTiles(other, ocean, other.State.MapId,
                            other.State.X, other.State.Y).Contains((s.MapId, x, y)));
                    var painted = ShipDock.Tiles(attributes).Any();
                    // A painted quay is an explicit safe shore: never choose unmarked sea around it.
                    var exit = painted ? ShipDock.Nearest(attributes, s.X, s.Y, 5, ExitClear) :
                        ShipDisembarkation.Find(s,d,Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight,ExitClear);
                    if (exit == null)
                    { Notice(player, "No free exit near this dock. Move the ship slightly and try again."); return; }
                    ReleaseLocked(player);
                    dock = (s.MapId,exit.Value.X,exit.Value.Y);
                }
                else
                {
                    var vessel = Ships.Values.FirstOrDefault(v => v.OwnerId == Guid.Empty && v.State.CaptainId == Guid.Empty &&
                        v.State.MapId == player.MapId &&
                        (Distance(v.State.X, v.State.Y, player.X + .5, player.Y + .5) <= 1.5 ||
                        v.Definition.MapId == player.MapId &&
                        Distance(player.X + .5, player.Y + .5, v.Definition.DockX + .5, v.Definition.DockY + .5) <= 1.5 &&
                        Distance(v.State.X, v.State.Y, player.X + .5, player.Y + .5) <= 5));
                    if (vessel == null || !ClearHull(player, vessel, ocean, vessel.State.MapId, vessel.State.X, vessel.State.Y))
                    { Notice(player, "No available ship near this helm dock."); return; }
                    vessel.State.CaptainId = player.Id;
                    vessel.LastStep = 0; vessel.Controls.Reset();
                    player.CaptainedShipId = vessel.State.Id;
                    player.CastTime = 0; player.IsBlocking = false;
                    anchor = vessel.State;
                }
            }
            // Native map/instance operations must not run under the global naval lock.
            if (anchor != null)
            {
                WarpAnchor(player, anchor);
                if (player.MapId != anchor.MapId) { Release(player); return; }
                Notice(player, "Helm acquired. W/S: thrust/brake, A/D: turn, Space: anchor. /naval: leave at dock.");
            }
            else if (dock is { } landing)
            {
                player.Warp(landing.MapId, landing.X, landing.Y);
                Notice(player, "You have left the helm.");
            }
            lock (Gate) SendSnapshot(player);
        }
    }

    internal static void Input(Player player, ShipInputPacket packet, long now)
    {
        lock (player.EntityLock)
        lock (Gate)
        {
            if (packet.ShipId != player.CaptainedShipId ||
                !Ships.TryGetValue(packet.ShipId, out var vessel) || vessel.State.CaptainId != player.Id ||
                !vessel.Controls.Accept(vessel.State, player.Id, player.InGame && !player.IsDead && !player.HasPendingDeath, packet, now)) return;
            player.RegisterAfkActivity();
        }
    }

    internal static void Update(Player player, long now)
    {
        ShipState pendingWarp = null;
        Vessel rescueVessel = null;
        Guid oldMap = Guid.Empty;
        double oldX = 0, oldY = 0;
        lock (Gate)
        {
            if (!IsEnabled()) return;
            if (Ships.TryGetValue(player.CaptainedShipId, out var vessel))
            {
                var s = vessel.State;
                var ocean = OceanMapResolver.Maps();
                if (player.Client == null || player.IsDead || player.HasPendingDeath || !player.InGame ||
                    player.InstanceType != MapInstanceType.Overworld || player.MapId != s.MapId ||
                    !ocean.Contains(s.MapId) || MapController.Get(s.MapId) == null)
                    ReleaseLocked(player);
                else if (vessel.SunkUntil > 0)
                {
                    if (now >= vessel.SunkUntil) rescueVessel = vessel;
                }
                else
                {
                    var dt = vessel.LastStep == 0 ? 0 : (now - vessel.LastStep) / 1000d;
                    vessel.LastStep = now;
                    var input = vessel.Controls.ForTick(now, player.NavalBusy);
                    oldMap = s.MapId; oldX = s.X; oldY = s.Y;
                    ShipMovement.Step(s, vessel.Definition, input, dt, (map, x, y, heading) =>
                    {
                        var resolved = OceanMapResolver.Resolve(ocean, ref map, ref x, ref y) && ClearHull(player, vessel, ocean, map, x, y);
                        return (resolved, map, x, y);
                    });
                    if (oldMap != s.MapId) pendingWarp = s;
                    else { player.X = (int)Math.Floor(s.X); player.Y = (int)Math.Floor(s.Y); }
                }
            }
        }
        if (rescueVessel != null)
        {
            OwnedShip owned;
            lock (Gate)
            {
                owned = player.NavalFleet.FirstOrDefault(s => s.Id == rescueVessel.State.Id);
                ReleaseLocked(player);
            }
            if (owned != null) ReturnToHarbour(player, owned);
        }
        if (pendingWarp != null)
        {
            WarpAnchor(player, pendingWarp);
            if (player.MapId != pendingWarp.MapId)
                lock (Gate)
                { if (oldMap != Guid.Empty) { pendingWarp.MapId = oldMap; pendingWarp.X = oldX; pendingWarp.Y = oldY; } ReleaseLocked(player); }
        }
        lock (Gate)
        {
            if (now - player.NavalSnapshotTime < 100 || player.Client == null) return;
            player.NavalSnapshotTime = now;
            SendSnapshot(player);
        }
    }

    internal static void Release(Player player, bool returnToHarbour = false)
    {
        // Ordinary players need no naval cleanup (including constructor-free test fixtures).
        if (player.CaptainedShipId == Guid.Empty) return;
        lock (player.EntityLock)
        {
            OwnedShip owned = null;
            lock (Gate)
            {
                if (returnToHarbour && Ships.TryGetValue(player.CaptainedShipId, out var vessel) && vessel.OwnerId == player.Id)
                    owned = player.NavalFleet.FirstOrDefault(s => s.Id == vessel.State.Id);
                ReleaseLocked(player);
            }
            if (owned != null) ReturnToHarbour(player, owned);
        }
    }

    private static void WarpAnchor(Player player, ShipState state)
    {
        player.NavalWarpInProgress = true;
        try { player.Warp(state.MapId, (float)Math.Floor(state.X), (float)Math.Floor(state.Y)); }
        finally { player.NavalWarpInProgress = false; }
    }
    private static void ReleaseLocked(Player player)
    {
        if (Ships.TryGetValue(player.CaptainedShipId, out var vessel) && vessel.State.CaptainId == player.Id)
        {
            vessel.State.CaptainId = Guid.Empty; vessel.State.Speed = vessel.State.Throttle = 0;
            vessel.Controls.Reset();
            if (vessel.OwnerId != Guid.Empty)
            {
                var owned = player.NavalFleet.FirstOrDefault(s => s.Id == vessel.State.Id);
                if (owned != null) owned.AtSea = false;
                Ships.Remove(vessel.State.Id);
            } // Stored in the character fleet, not a public boat.
        }
        player.CaptainedShipId = Guid.Empty;
    }

    private static void SendSnapshot(Player player)
    {
        var ocean = OceanMapResolver.Maps();
        var visible = player.Map?.GetSurroundingMapIds(true).ToHashSet() ?? [];
        visible.Add(player.MapId);
        player.SendPacket(new NavalSnapshotPacket { Ships = player.InstanceType != MapInstanceType.Overworld ? [] :
            Ships.Values.Where(v => visible.Contains(v.State.MapId) && ocean.Contains(v.State.MapId) && MapController.Get(v.State.MapId) != null)
                .Select(v => new ShipSnapshot { Id = v.State.Id, CaptainId = v.State.CaptainId, MapId = v.State.MapId,
                    X = v.State.X, Y = v.State.Y, Heading = v.State.Heading, Speed = v.State.Speed,
                    Graphics = v.SunkUntil > 0 ? v.SinkingGraphics : v.State.DamageState > 0 ? v.Definition.DamagedGraphics : v.Definition.Graphics,
                    Frames = v.Definition.Frames, FrameMilliseconds = v.Definition.FrameMilliseconds,
                    Width = v.Definition.Width, Length = v.Definition.Length }).ToArray() });
    }

    // A conservative circumscribed disk contains the hull at every continuous heading.
    // Tile intersection (including on adjacent maps) deliberately favours safety over narrow channels.
    private static HashSet<(Guid Map, int X, int Y)> HullTiles(Vessel vessel, HashSet<Guid> ocean, Guid map, double x, double y)
    {
        var tiles = new HashSet<(Guid, int, int)>();
        foreach (var (tx, ty) in ShipFootprint.Tiles(vessel.Definition.Width, vessel.Definition.Length, x, y))
        {
            var id = map; double px = tx + .5, py = ty + .5;
            if (!OceanMapResolver.Resolve(ocean, ref id, ref px, ref py)) { tiles.Add((Guid.Empty, 0, 0)); continue; }
            tiles.Add((id, (int)Math.Floor(px), (int)Math.Floor(py)));
        }
        return tiles;
    }

    private static bool ClearHull(Player player, Vessel vessel, HashSet<Guid> ocean, Guid map, double x, double y)
    {
        var tiles = HullTiles(vessel, ocean, map, x, y);
        if (tiles.Any(t => !ClearTile(player, t.Map, t.X, t.Y, allowDock: false))) return false;
        return !Ships.Values.Any(other => other != vessel &&
            tiles.Overlaps(HullTiles(other, ocean, other.State.MapId, other.State.X, other.State.Y)));
    }

    private static bool ClearTile(Player player, Guid id, int x, int y, bool allowDock = true)
    {
        var map = MapController.Get(id);
        if (map?.Attributes == null || x < 0 || y < 0 || x >= map.Attributes.GetLength(0) || y >= map.Attributes.GetLength(1) ||
            !map.TryGetInstance(player.MapInstanceId, out var instance) || instance.IsDisposed) return false;
        var attribute = map.Attributes[x, y];
        if (IsBlockingAttribute(attribute) && !(allowDock && attribute is MapDockAttribute)) return false;
        if (instance.GetCachedEntities().Any(e => e != player && !e.IsPassable() && e.X == x && e.Y == y && e.Z == player.Z)) return false;
        if (instance.GlobalEventInstances.Values.SelectMany(e => e.GlobalPageInstance)
            .Any(e => e != null && !e.Passable && e.X == x && e.Y == y && e.Z == player.Z)) return false;
        return !player.NavalEventBlocked(map, x, y);
    }

    internal static bool IsBlockingAttribute(MapAttribute attribute) =>
        attribute is MapDockAttribute or MapBlockedAttribute or MapWarpAttribute or MapResourceAttribute or MapSlideAttribute or MapGrappleStoneAttribute ||
            attribute is MapAnimationAttribute { IsBlock: true } ||
            attribute is MapZDimensionAttribute;

    private static double Distance(double x, double y, double otherX, double otherY) =>
        Math.Sqrt((x - otherX) * (x - otherX) + (y - otherY) * (y - otherY));
    private static void Notice(Player player, string source) => PacketSender.SendChatMsg(player, source, ChatMessageType.Notice);
}
