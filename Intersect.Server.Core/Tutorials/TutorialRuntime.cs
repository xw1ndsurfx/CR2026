using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.Tutorials;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Variables;
using Newtonsoft.Json;
namespace Intersect.Server.Tutorials;
internal static class TutorialRuntime
{
    private static readonly object Gate = new();
    private static readonly string FileName = Path.Combine("resources", "tutorial.json");
    private static TutorialConfiguration? _current;
    private static string? _json, _revision;
    internal static TutorialConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null) return _current;
                // Invalid authored files surface an error rather than silently replacing content.
                var config = File.Exists(FileName) ? JsonConvert.DeserializeObject<TutorialConfiguration>(File.ReadAllText(FileName)) : TutorialConfiguration.CreateDefault();
                if (config is not { IsValid: true }) throw new InvalidDataException("Invalid tutorial configuration.");
                return _current = config;
            }
        }
    }
    internal static string Json { get { lock (Gate) return _json ??= JsonConvert.SerializeObject(Current); } }
    internal static string Revision { get { lock (Gate) return _revision ??= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json))); } }
    internal static void Save(string json, string expectedRevision)
    {
        if (json.Length > 1500000) throw new InvalidDataException("Configuration too large.");
        var config = JsonConvert.DeserializeObject<TutorialConfiguration>(json);
        if (config is not { IsValid: true }) throw new InvalidDataException("Check steps and English, French and Spanish translations.");
        foreach (var step in config.Steps)
        {
            if (step.CinematicEventId != Guid.Empty && EventDescriptor.Get(step.CinematicEventId) is not { CommonEvent: true }) throw new InvalidDataException("Select an existing common event for the cinematic.");
            if (step.Objective == TutorialObjective.ReachMap && Intersect.Server.Maps.MapController.Get(step.MapId) == null) throw new InvalidDataException("Select an existing map.");
            if (step.Objective == TutorialObjective.Variable && PlayerVariableDescriptor.Get(step.VariableId) == null) throw new InvalidDataException("Select an existing player variable.");
        }
        lock (Gate)
        {
            if (expectedRevision != Revision) throw new InvalidDataException("The tutorial changed. Reopen the editor before saving.");
            Directory.CreateDirectory("resources");
            File.WriteAllText(FileName + ".tmp", JsonConvert.SerializeObject(config, Formatting.Indented));
            File.Move(FileName + ".tmp", FileName, true); _current = config; _json = null; _revision = null;
        }
        Intersect.Server.Localization.ContentTranslationRuntime.QueueJson(json);
    }
}
