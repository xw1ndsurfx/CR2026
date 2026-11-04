using System.Text;
using Intersect.Core;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Professions;
using Intersect.GameObjects;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Exportation;

/// <summary>Server-owned trade route configuration, edited only by authenticated editors.</summary>
internal static class ExportationRuntime
{
    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string FilePath = Path.Combine("resources", "exportation.json");
    private const int MaximumBytes = 262144;
    private static ExportationConfiguration? _active;

    internal static ExportationConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_active != null) return _active;
                try { return _active = Read(); }
                catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or
                    System.Text.Json.JsonException or ArgumentException)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(error,
                        "Invalid Exportation configuration. Preserving saved file and disabling the activity.");
                    return _active = new ExportationConfiguration { Enabled = false };
                }
            }
        }
    }

    private static ExportationConfiguration Read()
    {
        if (!File.Exists(FilePath)) return new ExportationConfiguration { Enabled = false };
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Exportation configuration too large.");
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        return Validate(reader.ReadToEnd());
    }

    /// <summary>Write to a same-directory temporary file before atomically activating the new config.</summary>
    internal static string Save(string json)
    {
        var candidate = Validate(json);
        var saved = candidate.ToJson();
        RequireBounded(saved);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(Utf8.GetBytes(saved));
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temp, FilePath, overwrite: true);
                _active = candidate;
                return saved;
            }
            finally
            {
                try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void RequireBounded(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumBytes ||
            Utf8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Invalid Exportation configuration size.");
    }

    private static ExportationConfiguration Validate(string json)
    {
        RequireBounded(json);
        var config = ExportationConfiguration.FromJson(json);
        if (!config.Enabled) return config;
        if (config.Routes.Length > 0 && (config.ProfessionId == Guid.Empty ||
            NativeProfessions.IsNative(config.ProfessionId) ||
            ProfessionConfigurationRuntime.Current.Find(config.ProfessionId) == null))
            throw new InvalidDataException("Select an existing custom Exportation profession before enabling routes.");

        foreach (var route in config.Routes)
        {
            if (NPCDescriptor.Get(route.SourceNpcId) is not { IsPet: false } ||
                NPCDescriptor.Get(route.RecipientNpcId) is not { IsPet: false } ||
                MapDescriptor.Get(route.SourceMapId) == null ||
                MapDescriptor.Get(route.RecipientMapId) == null ||
                QuestDescriptor.Get(route.QuestId) is not { Tasks.Count: > 0 } ||
                route.Lines.Any(line => ItemDescriptor.Get(line.ItemId) == null) ||
                (route.RewardQuantity > 0 &&
                    ItemDescriptor.Get(route.RewardItemId) is not { IsStackable: true, MaxInventoryStack: > 0 }))
                throw new InvalidDataException("Exportation route references an invalid NPC, island, quest or item.");
        }

        return config;
    }

    internal static ExportationRoute? Find(Guid id) =>
        Current.Enabled ? Current.Routes.FirstOrDefault(route => route.Id == id && route.IsValid) : null;
}
