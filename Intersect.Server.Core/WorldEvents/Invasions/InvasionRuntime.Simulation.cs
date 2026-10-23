using Intersect.Server.Maps;

namespace Intersect.Server.WorldEvents.Invasions;

internal static partial class InvasionRuntime
{
    // Published snapshots are never mutated. Map workers query them without taking
    // the invasion gate while holding map/entity locks.
    private static HashSet<Guid> _simulationMaps = [];
    private static long _nextSimulationRefreshAt;
    internal static IReadOnlyCollection<Guid> SimulationMapIds => Volatile.Read(ref _simulationMaps);
    internal static bool RequiresSimulation(Guid mapId) => Volatile.Read(ref _simulationMaps).Contains(mapId);

    private static void RefreshSimulationMaps()
    {
        var maps = new HashSet<Guid>();
        foreach (var session in SessionsById.Values.Where(session => !session.Completed))
        {
            foreach (var objective in session.Objectives) maps.Add(objective.MapId);
            foreach (var spawn in session.Definition.Waves.SelectMany(wave => wave.Spawns)) maps.Add(spawn.SpawnMapId);
            foreach (var npc in session.ActiveNpcs.Values.Where(npc => !npc.IsDead && !npc.IsDisposed)) maps.Add(npc.MapId);
        }
        // Keep neighboring overworld maps ready as invaders cross map borders.
        foreach (var mapId in maps.ToArray())
            if (MapController.Get(mapId) is { } map)
                foreach (var neighbor in map.GetSurroundingMapIds().Concat(new[] { map.Up, map.Down, map.Left, map.Right }))
                    if (neighbor != Guid.Empty) maps.Add(neighbor);
        maps.Remove(Guid.Empty);
        Volatile.Write(ref _simulationMaps, maps);
    }
}
