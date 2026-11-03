using Intersect.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Maps;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Intersect.Server.Fishing;

internal static class FishingRuntime
{
    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static FishingConfiguration? _current;
    private const string ConfigPath = "resources/fishing.json";
    private const string DraftPath = "resources/fishing.draft.json";
    internal const int MaximumConfigurationBytes = 1000000;
    internal const string InvalidDraftMessage = "The saved fishing draft is invalid. It was preserved and was not applied.";
    internal static FishingConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null) return _current;
                try
                {
                    var loaded = File.Exists(ConfigPath) ? Parse(ReadBoundedFile(ConfigPath)) : new FishingConfiguration();
                    Validate(loaded);
                    _current = loaded;
                }
                catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(error, "Fishing configuration could not be loaded; fishing is disabled.");
                    _current = new();
                }
                return _current;
            }
        }
    }

    internal static FishingConfiguration ReadDraft()
    {
        lock (Gate)
        {
            try
            {
                var draft = Parse(ReadBoundedFile(DraftPath));
                FishingRules.ValidateDraft(draft);
                return draft;
            }
            catch (FileNotFoundException) { return FishingConfiguration.FromJson(Serialize(Current)); }
            catch (DirectoryNotFoundException) { return FishingConfiguration.FromJson(Serialize(Current)); }
            catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException or ArgumentException)
            {
                // Preserve the file for recovery; never silently replace it with the active configuration.
                throw new InvalidDataException(InvalidDraftMessage, error);
            }
        }
    }

    internal static string SaveDraft(string json)
    {
        lock (Gate)
        {
            var draft = Parse(json);
            FishingRules.ValidateDraft(draft);
            var saved = Serialize(draft);
            Directory.CreateDirectory("resources");
            WriteAtomically(DraftPath, saved);
            return saved;
        }
    }

    internal static string Save(string json)
    {
        lock (Gate)
        {
            var config = Parse(json);
            Validate(config);
            var saved = Serialize(config);
            Directory.CreateDirectory("resources");
            // Validate everything before either write. If applying fails, the saved
            // draft remains recoverable and the previous active configuration stays in use.
            WriteAtomically(DraftPath, saved);
            WriteAtomically(ConfigPath, saved);
            _current = config;
            return saved;
        }
    }

    private static FishingConfiguration Parse(string json)
    {
        RequireBoundedJson(json);
        return FishingConfiguration.FromJson(json);
    }

    private static string Serialize(FishingConfiguration config)
    {
        var json = config.ToJson();
        RequireBoundedJson(json);
        return json;
    }

    private static void RequireBoundedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumConfigurationBytes ||
            Utf8.GetByteCount(json) > MaximumConfigurationBytes)
            throw new InvalidDataException("Invalid fishing configuration.");
    }

    private static string ReadBoundedFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumConfigurationBytes) throw new InvalidDataException("Invalid fishing configuration.");
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        var json = reader.ReadToEnd();
        RequireBoundedJson(json);
        return json;
    }

    private static void WriteAtomically(string path, string json)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Utf8.GetBytes(json));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static ProfessionDefinition? Profession(FishingConfiguration config) =>
        config.ProfessionId != Guid.Empty ? ProfessionConfigurationRuntime.Current.Find(config.ProfessionId) :
        ProfessionConfigurationRuntime.Current.Professions.FirstOrDefault(p =>
            !NativeProfessions.IsNative(p.Id) && new[] { "fishing", "pêche", "peche", "pêcheur", "pecheur", "pesca" }
                .Contains(p.Name.Trim().ToLowerInvariant()));

    private static void Validate(FishingConfiguration config)
    {
        FishingRules.Validate(config);
        if (config.ProfessionId != Guid.Empty && (Profession(config) == null || NativeProfessions.IsNative(config.ProfessionId))) throw Missing();
        foreach (var tool in config.Tools)
        {
            if (ItemDescriptor.Get(tool.ItemId) is not { ItemType: ItemType.Equipment } item ||
                item.EquipmentSlot < 0 || item.EquipmentSlot >= Options.Instance.Equipment.Slots.Count ||
                tool.BaitItemId != Guid.Empty && ItemDescriptor.Get(tool.BaitItemId) is not { IsStackable: true }) throw Missing();
            foreach (var animation in new[] { tool.CastAnimationId, tool.UseAnimationId, tool.CatchAnimationId })
                if (animation != Guid.Empty && AnimationDescriptor.Get(animation) == null) throw Missing();
        }
        foreach (var zone in config.Zones)
        {
            if (zone.MapIds.Any(mapId => MapController.Get(mapId) == null) ||
                zone.UseTileRadius && zone.MapIds.Count > 0 &&
                (zone.X >= Options.Instance.Map.MapWidth || zone.Y >= Options.Instance.Map.MapHeight)) throw Missing();
            foreach (var entry in zone.Catches)
            {
                if (ItemDescriptor.Get(entry.ItemId) is not { IsStackable: true, MaxInventoryStack: > 0 } ||
                    entry.RequiredVariableId != Guid.Empty && PlayerVariableDescriptor.Get(entry.RequiredVariableId) == null) throw Missing();
            }
        }
    }
    private static InvalidDataException Missing() => new("Fishing configuration references a missing item, map, profession or animation.");
}
