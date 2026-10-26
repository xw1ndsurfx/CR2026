using System.Collections.Concurrent;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;
namespace Intersect.Server.Jousting;
internal static class JoustAudienceRuntime
{
    internal readonly record struct Key(Guid Arena, Guid Instance);
    internal sealed class Slot
    {
        internal readonly object Gate = new();
        internal Lease? Member;
        internal JoustAudienceActor? Filler;
    }
    internal sealed class Context(Key key, MapInstance instance)
    {
        internal readonly object Lifecycle = new();
        internal bool Retired;
        internal readonly Key Key = key;
        internal readonly MapInstance Instance = instance;
        internal readonly ConcurrentDictionary<Guid, Slot> Slots = new();
        internal JoustArena? Arena => JoustRuntime.Current.Arenas.FirstOrDefault(a => a.Enabled && a.Id == Key.Arena);
    }
    internal sealed class Lease(Player player, Context context, JoustSeat seat)
    {
        internal readonly Guid Id = Guid.NewGuid();
        internal readonly Player Player = player;
        internal readonly Context Context = context;
        internal readonly JoustSeat Seat = System.Text.Json.JsonSerializer.Deserialize<JoustSeat>(System.Text.Json.JsonSerializer.Serialize(seat))!;
        internal readonly Intersect.Enums.Direction BeforeDirection = player.Dir;
        internal JoustSpectatorCamera Camera;
        internal Guid CameraMatch;
        internal JoustSpectatorCamera? AppliedCamera;
        internal long CameraSent;
        internal long ViewSent;
    }
    private sealed record Broadcast(JoustBroadcast View, long Published);
    private static readonly ConcurrentDictionary<Key, Context> Contexts = new();
    private static readonly ConcurrentDictionary<Guid, Lease> Members = new();
    private static readonly ConcurrentDictionary<Key, Broadcast> Matches = new();
    private static readonly System.Threading.Timer Timer = new(_ => Sweep(), null, 200, 200);
    private static long _crowdAt;
    private static int _sweeping;
    internal static Context? Get(JoustArena arena, Guid instance)
    {
        var key = new Key(arena.Id, instance);
        if (Contexts.TryGetValue(key, out var context)) return context;
        if (Contexts.Count >= 128 || !MapController.TryGetInstanceFromMap(arena.MapId, instance, out var map)) return null;
        return Contexts.GetOrAdd(key, _ => new(key, map));
    }
    internal static bool SeatTileClear(Context context, JoustSeat seat, Entity? ignored = null)
    {
        var arena = context.Arena; var map = arena == null ? null : MapController.Get(arena.MapId);
        if (map == null || seat.X >= Intersect.Options.Instance.Map.MapWidth || seat.Y >= Intersect.Options.Instance.Map.MapHeight) return false;
        if (map.Attributes[seat.X, seat.Y] is { } attribute && attribute is not MapBlockedAttribute) return false;
        return !context.Instance.GetEntities().Any(e => e != ignored && e is not { IsDisposed: true } && !e.IsDead && !e.IsPassable() && e.X == seat.X && e.Y == seat.Y);
    }
    internal static Lease? Reserve(Player player, JoustArena arena, JoustSeat seat)
    {
        var context = Get(arena, player.MapInstanceId); if (context == null) return null;
        var slot = context.Slots.GetOrAdd(seat.Id, _ => new());
        lock (context.Lifecycle)
        lock (slot.Gate)
        {
            if (context.Retired || slot.Member != null || !SeatTileClear(context, seat, slot.Filler)) return null;
            var lease = new Lease(player, context, seat);
            if (!Members.TryAdd(player.Id, lease)) return null;
            slot.Member = lease;
            RemoveFiller(context, slot);
            return lease;
        }
    }
    internal static void Release(Lease lease)
    {
        if (lease.Context.Slots.TryGetValue(lease.Seat.Id, out var slot))
            lock (slot.Gate) { if (ReferenceEquals(slot.Member, lease)) slot.Member = null; }
        ((ICollection<KeyValuePair<Guid, Lease>>)Members).Remove(new(lease.Player.Id, lease));
    }
    internal static JoustSpectatorView View(Context context, Player player)
    {
        var arena = context.Arena;
        if (arena == null) return new();
        var own = Members.GetValueOrDefault(player.Id);
        return new() { ArenaId = arena.Id, ArenaName = arena.Name,
            Seats = arena.Seats.Select(s => {
                Lease? occupant = null;
                if (context.Slots.TryGetValue(s.Id, out var slot)) lock (slot.Gate) occupant = slot.Member;
                return new JoustSeatView(s.Id, s.Name, s.X, s.Y, s.ExitX, s.ExitY, occupant != null, occupant?.Player.Name ?? "");
            }).ToArray(), CurrentSeat = own != null && own.Context == context ? own.Seat.Id : Guid.Empty,
            Camera = own?.Camera ?? JoustSpectatorCamera.Arena, Match = CurrentMatch(context.Key) };
    }
    internal static JoustBroadcast? CurrentMatch(Key key) => Matches.TryGetValue(key, out var value) && Environment.TickCount64 - value.Published < (value.View.Live ? 5000 : 20000) ? value.View : null;
    internal static void Publish(JoustArena arena, Guid instance, JoustMatch match, Guid firstId, string firstName, Guid secondId, string secondName)
    {
        if (!JoustRuntime.Owns(arena.Id, firstId, match.Id)) return;
        Matches[new(arena.Id, instance)] = new(new JoustBroadcast { MatchId = match.Id, FirstId = firstId, SecondId = secondId,
            FirstName = firstName, SecondName = secondName, IsPve = match.IsPve, Phase = match.Phase, Pass = match.Pass, Passes = match.Passes,
            FirstScore = match.Scores[0], SecondScore = match.Scores[1], Winner = match.Winner }, Environment.TickCount64);
        Get(arena, instance);
    }
    internal static void EnsureCrowd(Context context)
    { lock (context.Lifecycle) { if (!context.Retired) EnsureCrowdCore(context); } }
    private static void EnsureCrowdCore(Context context)
    {
        if (context.Retired) return;
        var arena = context.Arena;
        foreach (var pair in context.Slots)
            if (arena == null || !arena.Seats.Any(s => s.Id == pair.Key))
                lock (pair.Value.Gate) { RemoveFiller(context, pair.Value); if (pair.Value.Member == null) context.Slots.TryRemove(pair.Key, out _); }
        if (arena == null || context.Instance.IsDisposed) return;
        var npc = NPCDescriptor.Get(arena.CrowdNpcId);
        foreach (var seat in arena.Seats)
        {
            var slot = context.Slots.GetOrAdd(seat.Id, _ => new());
            lock (slot.Gate)
            {
                if (slot.Filler is { } previous && (npc == null || previous.DefinitionId != npc.Id || previous.X != seat.X || previous.Y != seat.Y || (int)previous.Dir != seat.Facing)) RemoveFiller(context, slot);
                if (slot.Member != null || slot.Filler != null || npc == null || string.IsNullOrWhiteSpace(npc.Sprite) || !SeatTileClear(context, seat)) continue;
                var filler = new JoustAudienceActor(npc, arena.MapId, context.Key.Instance, seat.X, seat.Y, seat.Facing);
                slot.Filler = filler; context.Instance.AddEntity(filler); PacketSender.SendEntityDataToProximity(filler);
            }
        }
    }
    private static void RemoveFiller(Context context, Slot slot)
    {
        if (slot.Filler is not { } filler) return;
        slot.Filler = null; context.Instance.RemoveEntity(filler); filler.Dispose(); PacketSender.SendEntityLeave(filler);
    }
    internal static void Sweep()
    {
        if (Interlocked.Exchange(ref _sweeping, 1) != 0) return;
        try { SweepCore(); } finally { Volatile.Write(ref _sweeping, 0); }
    }
    private static void SweepCore()
    {
        foreach (var lease in Members.Values)
        {
            if (!Monitor.TryEnter(lease.Player.EntityLock)) continue;
            try { lease.Player.UpdateJoustSeat(); }
            catch (Exception e) { Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(e, "Joust spectator cleanup failed"); }
            finally { Monitor.Exit(lease.Player.EntityLock); }
        }
        var now = Environment.TickCount64; if (now < Interlocked.Read(ref _crowdAt)) return;
        Interlocked.Exchange(ref _crowdAt, now + 1000);
        foreach (var context in Contexts.Values)
        {
            try
            {
                var arena = context.Arena;
                var active = arena != null && MapController.Get(arena.MapId) != null && !context.Instance.IsDisposed &&
                    context.Instance.GetEntities().OfType<Player>().Any(p => p.InGame && p.Client != null);
                if (active) EnsureCrowd(context);
                else
                {
                    lock (context.Lifecycle)
                    {
                        foreach (var slot in context.Slots.Values) lock (slot.Gate) RemoveFiller(context, slot);
                        if (!context.Slots.Values.Any(s => s.Member != null))
                        { context.Retired = true; ((ICollection<KeyValuePair<Key, Context>>)Contexts).Remove(new(context.Key, context)); }
                    }
                }
            }
            catch (Exception e) { Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(e, "Joust audience refresh failed"); }
        }
        foreach (var pair in Matches) if (now - pair.Value.Published > 20000) ((ICollection<KeyValuePair<Key, Broadcast>>)Matches).Remove(pair);
    }
}
