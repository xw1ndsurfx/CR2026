using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

/// <summary>Opt-in overworld prototype. All mutation happens under the captain's entity lock then Gate.</summary>
internal static class NavalSystem
{
    private sealed class Settings
    {
        public bool Enabled { get; set; }
        public ShipDefinition[] Ships { get; set; } = [];
    }
    private sealed class Vessel(ShipDefinition definition)
    {
        internal readonly ShipDefinition Definition = definition;
        internal readonly ShipState State = new() { Id = definition.Id, MapId = definition.MapId,
            X = definition.X, Y = definition.Y, Heading = ShipMovement.Normalize(definition.Heading) };
        internal readonly ShipInputAuthority Controls = new();
        internal long LastStep;
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Vessel> Ships = [];
    private static readonly Lazy<bool> Enabled = new(() =>
    {
        // Missing/invalid configuration fails closed; never create or modify live map data.
        var path = Path.Combine("resources", "naval.json");
        if (!File.Exists(path)) return false;
        var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
        if (settings is not { Enabled: true } || settings.Ships == null || settings.Ships.Length > 64 ||
            settings.Ships.Any(d => d == null || !d.IsValid() || d.X >= Options.Instance.Map.MapWidth ||
                d.Y >= Options.Instance.Map.MapHeight || d.DockX >= Options.Instance.Map.MapWidth || d.DockY >= Options.Instance.Map.MapHeight) ||
            settings.Ships.Select(d => d.Id).Distinct().Count() != settings.Ships.Length) return false;
        foreach (var definition in settings.Ships) Ships.Add(definition.Id, new Vessel(definition));
        return true;
    });
    private static bool IsEnabled()
    {
        try { return Enabled.Value; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return false; }
    }

    internal static void Helm(Player player)
    {
        lock (player.EntityLock)
        {
            ShipState anchor = null;
            ShipDefinition dock = null;
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
                    if (s.MapId != d.MapId || Math.Abs(s.Speed) > .1 ||
                        Distance(s.X, s.Y, d.DockX + .5, d.DockY + .5) > 5 ||
                        !ClearTile(player, d.MapId, d.DockX, d.DockY) ||
                        HullTiles(active, ocean, s.MapId, s.X, s.Y).Contains((d.MapId, d.DockX, d.DockY)))
                    { Notice(player, "Stop near the helm dock to disembark safely."); return; }
                    ReleaseLocked(player);
                    dock = d;
                }
                else
                {
                    var vessel = Ships.Values.FirstOrDefault(v => v.State.CaptainId == Guid.Empty &&
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
            else if (dock != null)
            {
                player.Warp(dock.MapId, dock.DockX, dock.DockY);
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
        if (pendingWarp != null)
        {
            WarpAnchor(player, pendingWarp);
            if (player.MapId != pendingWarp.MapId)
                lock (Gate)
                { pendingWarp.MapId = oldMap; pendingWarp.X = oldX; pendingWarp.Y = oldY; ReleaseLocked(player); }
        }
        lock (Gate)
        {
            if (now - player.NavalSnapshotTime < 100 || player.Client == null) return;
            player.NavalSnapshotTime = now;
            SendSnapshot(player);
        }
    }

    internal static void Release(Player player)
    {
        // Ordinary players need no naval cleanup (including constructor-free test fixtures).
        if (player.CaptainedShipId == Guid.Empty) return;
        lock (player.EntityLock)
        lock (Gate) ReleaseLocked(player);
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
                    Graphics = v.State.DamageState > 0 ? v.Definition.DamagedGraphics : v.Definition.Graphics,
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
        if (tiles.Any(t => !ClearTile(player, t.Map, t.X, t.Y))) return false;
        return !Ships.Values.Any(other => other != vessel &&
            tiles.Overlaps(HullTiles(other, ocean, other.State.MapId, other.State.X, other.State.Y)));
    }

    private static bool ClearTile(Player player, Guid id, int x, int y)
    {
        var map = MapController.Get(id);
        if (map?.Attributes == null || x < 0 || y < 0 || x >= map.Attributes.GetLength(0) || y >= map.Attributes.GetLength(1) ||
            !map.TryGetInstance(player.MapInstanceId, out var instance) || instance.IsDisposed) return false;
        var attribute = map.Attributes[x, y];
        if (IsBlockingAttribute(attribute)) return false;
        if (instance.GetCachedEntities().Any(e => e != player && !e.IsPassable() && e.X == x && e.Y == y && e.Z == player.Z)) return false;
        if (instance.GlobalEventInstances.Values.SelectMany(e => e.GlobalPageInstance)
            .Any(e => e != null && !e.Passable && e.X == x && e.Y == y && e.Z == player.Z)) return false;
        return !player.NavalEventBlocked(map, x, y);
    }

    internal static bool IsBlockingAttribute(MapAttribute attribute) =>
        attribute is MapBlockedAttribute or MapWarpAttribute or MapResourceAttribute or MapSlideAttribute or MapGrappleStoneAttribute ||
            attribute is MapAnimationAttribute { IsBlock: true } ||
            attribute is MapZDimensionAttribute;

    private static double Distance(double x, double y, double otherX, double otherY) =>
        Math.Sqrt((x - otherX) * (x - otherX) + (y - otherY) * (y - otherY));
    private static void Notice(Player player, string source) => PacketSender.SendChatMsg(player, source, ChatMessageType.Notice);
}
