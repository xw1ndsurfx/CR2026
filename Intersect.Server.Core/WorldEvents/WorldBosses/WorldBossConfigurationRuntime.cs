using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.WorldBosses;
using Intersect.Server.Maps;

namespace Intersect.Server.WorldEvents.WorldBosses;

internal static class WorldBossConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "world-bosses.json");
    private static WorldBossConfiguration? _current;

    internal static WorldBossConfiguration Current
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

    internal static bool IsValid(WorldBossConfiguration configuration)
    {
        if (!configuration.IsStructurallyValid)
            return false;

        foreach (var boss in configuration.Bosses)
        {
            if (NPCDescriptor.Get(boss.NpcId) is not { IsBoss: true } ||
                MapController.Get(boss.MapId) == null ||
                boss.SpawnX >= Options.Instance.Map.MapWidth ||
                boss.SpawnY >= Options.Instance.Map.MapHeight)
                return false;
        }

        return true;
    }

    internal static void Save(string json)
    {
        var configuration = WorldBossConfiguration.FromJson(json);
        if (!IsValid(configuration))
            throw new InvalidDataException(
                "World Boss configuration references an invalid map, tile, or NPC not marked as Boss."
            );

        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
        }
    }

    private static WorldBossConfiguration LoadCore()
    {
        if (!File.Exists(PathName))
            return new WorldBossConfiguration();

        try
        {
            var configuration = WorldBossConfiguration.FromJson(File.ReadAllText(PathName));
            return IsValid(configuration) ? configuration : new WorldBossConfiguration();
        }
        catch
        {
            return new WorldBossConfiguration();
        }
    }
}
