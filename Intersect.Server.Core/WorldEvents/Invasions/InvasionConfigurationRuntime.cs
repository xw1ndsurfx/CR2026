using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Server.Maps;

namespace Intersect.Server.WorldEvents.Invasions;

internal static class InvasionConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "invasions.json");
    private static InvasionConfiguration? _current;

    internal static InvasionConfiguration Current
    {
        get
        {
            lock (Gate)
                return _current ??= LoadCore();
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
                return Current.ToJson();
        }
    }

    internal static bool IsValid(InvasionConfiguration configuration)
    {
        if (!configuration.IsStructurallyValid)
            return false;

        foreach (var invasion in configuration.Invasions)
        {
            if (MapController.Get(invasion.TargetMapId) == null)
                return false;

            if (invasion.TargetEventId != Guid.Empty)
            {
                var targetEvent = EventDescriptor.Get(invasion.TargetEventId);
                if (targetEvent == null ||
                    targetEvent.CommonEvent ||
                    targetEvent.MapId != invasion.TargetMapId)
                    return false;
            }

            foreach (var wave in invasion.Waves)
            foreach (var spawn in wave.Spawns)
            {
                if (NPCDescriptor.Get(spawn.NpcId) == null ||
                    MapController.Get(spawn.SpawnMapId) == null)
                    return false;
            }
        }

        return true;
    }

    internal static void Save(string json)
    {
        var configuration = InvasionConfiguration.FromJson(json);
        if (!IsValid(configuration))
            throw new InvalidDataException("Invasion configuration references missing maps or NPCs.");

        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
        }
    }

    private static InvasionConfiguration LoadCore()
    {
        if (!File.Exists(PathName))
            return new InvasionConfiguration();

        try
        {
            var configuration = InvasionConfiguration.FromJson(File.ReadAllText(PathName));
            return IsValid(configuration) ? configuration : new InvasionConfiguration();
        }
        catch
        {
            return new InvasionConfiguration();
        }
    }
}
