using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;

namespace Intersect.Server.Professions;

internal static class ProfessionConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "professions.json");
    private static ProfessionConfiguration? _current;

    internal static ProfessionConfiguration Current
    {
        get { lock (Gate) return _current ??= LoadCore(); }
    }

    internal static string Json
    {
        get { lock (Gate) return Current.ToJson(); }
    }

    internal static bool IsValid(ProfessionConfiguration configuration)
    {
        if (!configuration.IsStructurallyValid) return false;
        foreach (var profession in configuration.Professions)
        {
            foreach (var resource in profession.Resources)
                if (ResourceDescriptor.Get(resource.ResourceId) == null) return false;

            foreach (var reward in profession.LevelRewards)
            {
                if (reward.ItemId != Guid.Empty && ItemDescriptor.Get(reward.ItemId) == null) return false;
                if (reward.EventId != Guid.Empty && EventDescriptor.Get(reward.EventId) is not { CommonEvent: true }) return false;
            }
        }
        return true;
    }

    internal static void Save(string json)
    {
        var configuration = ProfessionConfiguration.FromJson(json);
        if (!IsValid(configuration))
            throw new InvalidDataException("Profession configuration references missing resources, items, or common events.");

        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
            ProfessionConfiguration.Load(configuration.ToJson());
        }
    }

    private static ProfessionConfiguration LoadCore()
    {
        try
        {
            var configuration = File.Exists(PathName)
                ? ProfessionConfiguration.FromJson(File.ReadAllText(PathName))
                : new ProfessionConfiguration();
            if (!IsValid(configuration)) configuration = new ProfessionConfiguration();
            ProfessionConfiguration.Load(configuration.ToJson());
            return configuration;
        }
        catch
        {
            var empty = new ProfessionConfiguration();
            ProfessionConfiguration.Load(empty.ToJson());
            return empty;
        }
    }
}
