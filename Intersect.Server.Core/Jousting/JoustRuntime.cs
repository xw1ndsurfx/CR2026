using Intersect.Framework.Core.Jousting;
namespace Intersect.Server.Jousting;
internal static class JoustRuntime
{
    internal static bool TileClear(Intersect.Server.Maps.MapInstance instance, int x, int y, Intersect.Server.Entities.Entity? actor)
    {
        if (x < 0 || y < 0 || x >= Intersect.Options.Instance.Map.MapWidth || y >= Intersect.Options.Instance.Map.MapHeight) return false;
        // A scripted lane must not trigger warps, slides, traps or story Events during a charge.
        var map = Intersect.Server.Maps.MapController.Get(actor?.MapId ?? Guid.Empty);
        if (map == null || map.Attributes[x, y] != null) return false;
        return !instance.GetEntities().Any(e => e != actor && !e.IsDisposed && !e.IsDead && e.X == x && e.Y == y && !e.IsPassable());
    }
    internal static bool LanesClear(Intersect.Framework.Core.Jousting.JoustArena arena, Guid instanceId, Intersect.Server.Entities.Player owner)
    {
        if (!Intersect.Server.Maps.MapController.TryGetInstanceFromMap(arena.MapId, instanceId, out var instance)) return false;
        for (var x = Math.Min(arena.StartX, arena.EndX); x <= Math.Max(arena.StartX, arena.EndX); x++)
            if (!TileClear(instance, x, arena.StartY, owner) || !TileClear(instance, x, arena.OpponentY, owner)) return false;
        return true;
    }
    private static readonly object Gate = new();
    private static JoustConfiguration? _current;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Guid> Occupied = new();
    internal static bool Reserve(Guid arena, Guid player) => Occupied.TryAdd(arena, player);
    internal static void Release(Guid arena, Guid player) =>
        ((ICollection<KeyValuePair<Guid, Guid>>)Occupied).Remove(new(arena, player));
    internal static JoustConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null) return _current;
                try { _current = File.Exists("resources/jousting.json") ? JoustConfiguration.FromJson(File.ReadAllText("resources/jousting.json")) : new(); }
                catch (Exception) { _current = new(); }
                return _current;
            }
        }
    }
    internal static void Save(string json)
    {
        var config = JoustConfiguration.FromJson(json);
        if (config.Economy.Offers.Any(o => !Intersect.Framework.Core.MiniGames.MiniGameCurrency.IsCompatible(Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(o.ItemId))))
            throw new InvalidDataException("joust-reward-item");
        foreach (var arena in config.Arenas.Where(a => a.Enabled))
        {
            if (Intersect.Server.Maps.MapController.Get(arena.MapId) == null ||
                Math.Max(arena.StartX, arena.EndX) >= Intersect.Options.Instance.Map.MapWidth ||
                arena.StartY >= Intersect.Options.Instance.Map.MapHeight || arena.OpponentY >= Intersect.Options.Instance.Map.MapHeight ||
                Intersect.Server.Mounts.MountRuntime.Find(arena.MountId) is not { IntegratedRider: true })
                throw new InvalidDataException("joust-arena-resources");
            foreach (var id in new[] { arena.IntroductionEventId, arena.ChargeEventId, arena.ImpactEventId, arena.UnhorseEventId, arena.VictoryEventId }.Where(i => i != Guid.Empty))
                if (Intersect.Framework.Core.GameObjects.Events.EventDescriptor.Get(id) is not { CommonEvent: true } &&
                    !Intersect.Server.Entities.Events.CommandProcessing.TryResolveCinematicScene(Intersect.Server.Maps.MapController.Get(arena.MapId), id, out _))
                    throw new InvalidDataException("joust-event-resource");
        }
        lock (Gate)
        {
            Directory.CreateDirectory("resources");
            File.WriteAllText("resources/jousting.json.tmp", config.ToJson());
            File.Move("resources/jousting.json.tmp", "resources/jousting.json", true); _current = config;
        }
    }
}
