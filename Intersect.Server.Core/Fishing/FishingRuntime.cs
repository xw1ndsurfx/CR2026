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
        if (config.ProfessionId != Guid.Empty)
        {
            if (NativeProfessions.IsNative(config.ProfessionId))
                throw FishingValidationDiagnostic.Create(
                    "Native profession {0} cannot be used for fishing. Select a custom profession or automatic detection / built-in progression.", config.ProfessionId);
            if (Profession(config) == null)
                throw FishingValidationDiagnostic.Create(
                    "Fishing profession {0} does not exist on the server. Save it in the Profession Editor or select automatic detection / built-in progression.", config.ProfessionId);
        }
        for (var index = 0; index < config.Tools.Count; index++)
        {
            var tool = config.Tools[index];
            var item = ItemDescriptor.Get(tool.ItemId);
            if (item == null)
                throw FishingValidationDiagnostic.Create(
                    "Fishing tool #{0}: equipment item {1} does not exist on the server. Select an existing equipment item.", index + 1, tool.ItemId);
            var toolReference = FishingValidationDiagnostic.Reference(item.Name, item.Id);
            if (item.ItemType != ItemType.Equipment)
                throw FishingValidationDiagnostic.Create(
                    "Fishing tool #{0}: item {1} is not equipment. Choose an equipment item.", index + 1, toolReference);
            if (item.EquipmentSlot < 0 || item.EquipmentSlot >= Options.Instance.Equipment.Slots.Count)
                throw FishingValidationDiagnostic.Create(
                    "Fishing tool #{0}: item {1} uses equipment slot {2}, but the server defines {3} slots. Correct its slot in the Item Editor.",
                    index + 1, toolReference, item.EquipmentSlot, Options.Instance.Equipment.Slots.Count);
            if (tool.BaitItemId != Guid.Empty)
            {
                var bait = ItemDescriptor.Get(tool.BaitItemId);
                if (bait == null)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing tool #{0} ({1}): bait item {2} does not exist on the server.", index + 1, toolReference, tool.BaitItemId);
                if (!bait.IsStackable)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing tool #{0} ({1}): bait item {2} must be stackable. Correct the item or choose another bait.",
                        index + 1, toolReference, FishingValidationDiagnostic.Reference(bait.Name, bait.Id));
            }
            foreach (var (animation, source) in new[]
            {
                (tool.CastAnimationId, "Fishing tool #{0} ({1}): cast animation {2} does not exist on the server."),
                (tool.UseAnimationId, "Fishing tool #{0} ({1}): use animation {2} does not exist on the server."),
                (tool.CatchAnimationId, "Fishing tool #{0} ({1}): catch animation {2} does not exist on the server."),
            })
                if (animation != Guid.Empty && AnimationDescriptor.Get(animation) == null)
                    throw FishingValidationDiagnostic.Create(source, index + 1, toolReference, animation);
        }
        foreach (var zone in config.Zones)
        {
            var zoneReference = FishingValidationDiagnostic.Reference(zone.Name, zone.Id);
            foreach (var mapId in zone.MapIds)
                if (MapController.Get(mapId) == null)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing zone {0}: map {1} does not exist on the server. Remove or replace that map selection.", zoneReference, mapId);
            if (zone.UseTileRadius && zone.MapIds.Count > 0 &&
                (zone.X >= Options.Instance.Map.MapWidth || zone.Y >= Options.Instance.Map.MapHeight))
                throw FishingValidationDiagnostic.Create(
                    "Fishing zone {0}: tile center ({1}, {2}) is outside the server map dimensions ({3} by {4}). Correct the coordinates or use whole maps.",
                    zoneReference, zone.X, zone.Y, Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
            for (var index = 0; index < zone.Catches.Count; index++)
            {
                var entry = zone.Catches[index];
                var catchReference = $"#{index + 1} ({entry.Id})";
                var reward = ItemDescriptor.Get(entry.ItemId);
                if (reward == null)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing zone {0}, catch {1}: reward item {2} does not exist on the server.", zoneReference, catchReference, entry.ItemId);
                var rewardReference = FishingValidationDiagnostic.Reference(reward.Name, reward.Id);
                if (reward.IsStackable && reward.MaxInventoryStack <= 0)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing zone {0}, catch {1}: reward item {2} has maximum inventory stack {3}. Set a value greater than zero.",
                        zoneReference, catchReference, rewardReference, reward.MaxInventoryStack);
                foreach (var loot in entry.BonusLoot)
                {
                    var bonus = ItemDescriptor.Get(loot.ItemId);
                    if (bonus == null || bonus.IsStackable && bonus.MaxInventoryStack <= 0)
                        throw FishingValidationDiagnostic.Create(
                            "Fishing zone {0}, catch {1}: bonus loot item {2} is missing or has an invalid inventory stack.",
                            zoneReference, catchReference, loot.ItemId);
                }
                if (entry.RequiredVariableId != Guid.Empty && PlayerVariableDescriptor.Get(entry.RequiredVariableId) == null)
                    throw FishingValidationDiagnostic.Create(
                        "Fishing zone {0}, catch {1}: required player variable {2} does not exist on the server.", zoneReference, catchReference, entry.RequiredVariableId);
            }
        }
    }
}
