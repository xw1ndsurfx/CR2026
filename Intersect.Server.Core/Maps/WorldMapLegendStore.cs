using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.WorldMaps;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Maps;

internal static class WorldMapLegendStore
{
    private static readonly object Gate = new();
    private static readonly string FileName = Path.Combine("resources", "worldmap-legend.json");
    private static string? _json;

    internal static string Json
    {
        get
        {
            lock (Gate)
            {
                if (_json != null) return _json;
                try
                {
                    _json = File.Exists(FileName)
                        ? WorldMapLegend.FromJson(File.ReadAllText(FileName)).ToJson()
                        : new WorldMapLegend().ToJson();
                }
                catch (Exception exception)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(exception, "Unable to load World Map legend");
                    _json = new WorldMapLegend().ToJson();
                }
                return _json;
            }
        }
    }

    internal static void Save(string json)
    {
        var legend = WorldMapLegend.FromJson(json);
        foreach (var entry in legend.Entries)
        {
            if (AnimationDescriptor.Get(entry.AnimationId) == null)
                throw new InvalidDataException($"Missing legend animation: {entry.AnimationId}");
        }
        var normalized = legend.ToJson();
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            File.WriteAllText(FileName + ".tmp", normalized);
            File.Move(FileName + ".tmp", FileName, overwrite: true);
            _json = normalized;
        }
    }
}
