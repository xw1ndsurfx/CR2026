using Intersect.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Maps;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Fishing;

internal static class FishingRuntime
{
    private static readonly object Gate = new();
    private static FishingConfiguration? _current;
    private const string ConfigPath = "resources/fishing.json";
    internal static FishingConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null) return _current;
                try
                {
                    _current = File.Exists(ConfigPath) ? FishingConfiguration.FromJson(File.ReadAllText(ConfigPath)) : new();
                    Validate(_current);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(error, "Fishing configuration could not be loaded; fishing is disabled.");
                    _current = new();
                }
                return _current;
            }
        }
    }

    internal static void Save(string json)
    {
        if (json is not { Length: > 0 and <= 1000000 }) throw new InvalidDataException("Invalid fishing configuration.");
        var config = FishingConfiguration.FromJson(json);
        Validate(config);
        lock (Gate)
        {
            Directory.CreateDirectory("resources");
            File.WriteAllText(ConfigPath + ".tmp", config.ToJson());
            File.Move(ConfigPath + ".tmp", ConfigPath, true);
            _current = config;
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
            if (zone.Enabled && (MapController.Get(zone.MapId) == null || zone.X >= Options.Instance.Map.MapWidth || zone.Y >= Options.Instance.Map.MapHeight)) throw Missing();
            foreach (var entry in zone.Catches)
            {
                if (ItemDescriptor.Get(entry.ItemId) is not { IsStackable: true, MaxInventoryStack: > 0 } ||
                    entry.RequiredVariableId != Guid.Empty && PlayerVariableDescriptor.Get(entry.RequiredVariableId) == null) throw Missing();
            }
        }
    }
    private static InvalidDataException Missing() => new("Fishing configuration references a missing item, map, profession or animation.");
}
