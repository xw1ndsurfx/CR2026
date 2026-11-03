using Intersect.Framework.Core.Systems.Deaths;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Server.Maps;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Systems.Deaths;

internal static class DeathConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string FileName = Path.Combine("resources", "deaths.json");
    private static DeathConfiguration? _current;
    internal static DeathConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null) return _current;
                try { _current = File.Exists(FileName) ? DeathConfiguration.FromJson(File.ReadAllText(FileName)) : new(); }
                catch (Exception exception)
                {
                    Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(exception, "Cannot load death configuration; using normal respawns.");
                    _current = new();
                }
                return _current;
            }
        }
    }
    internal static void Save(string json)
    {
        var config = DeathConfiguration.FromJson(json);
        foreach (var rule in config.Rules)
        {
            if (MapController.Get(rule.RespawnMapId) == null || rule.MapIds.Any(id => MapController.Get(id) == null))
                throw new InvalidDataException("A selected map no longer exists.");
            if (rule.CommonEventId != Guid.Empty && EventDescriptor.Get(rule.CommonEventId) is not { CommonEvent: true })
                throw new InvalidDataException("Choose a common event.");
            if (rule.AnimationId != Guid.Empty && AnimationDescriptor.Get(rule.AnimationId) == null)
                throw new InvalidDataException("Choose an existing animation.");
            if (rule.GroundAnimationId != Guid.Empty && AnimationDescriptor.Get(rule.GroundAnimationId) == null)
                throw new InvalidDataException("Choose an existing animation.");
            if (DeathConfiguration.AnimationDuration(AnimationDescriptor.Get(rule.AnimationId)) > 60000)
                throw new InvalidDataException("Death animations must last no longer than 60 seconds, including repeats.");
        }
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            File.WriteAllText(FileName + ".tmp", config.ToJson());
            File.Move(FileName + ".tmp", FileName, true);
            _current = config;
        }
    }
}
