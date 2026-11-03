namespace Intersect.Framework.Core.Fishing;

/// <summary>Pure, shared validation and selection. Only the server supplies eligibility facts and randomness.</summary>
public static class FishingRules
{
    public const int MaximumMapsPerZone = 256;
    public static readonly Guid FallbackExperienceId = new("cb2bdf43-e565-4de3-a897-b9c9219b7031");
    public static readonly Guid LastRewardId = new("cb2bdf43-e565-4de3-a897-b9c9219b7032");

    public static bool SafeAssetPath(string? path, params string[] extensions) => path is not null &&
        (path.Length == 0 || path.Length <= 240 && !Path.IsPathRooted(path) && !path.Contains(':') &&
            !path.Contains('\\') && !path.Split('/').Any(part => part is "" or "." or "..") &&
            extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));

    public static void Validate(FishingConfiguration config) => ValidateConfiguration(config, draft: false);

    /// <summary>Checks safe draft structure without requiring authoring references to be complete.</summary>
    public static void ValidateDraft(FishingConfiguration config) => ValidateConfiguration(config, draft: true);

    private static void ValidateConfiguration(FishingConfiguration config, bool draft)
    {
        if (config is null || config.Version != 1 || config.Tools is null || config.Zones is null || config.Skills is null ||
            config.Tools.Count > 128 || config.Zones.Count > 512 || config.Skills.Count > 4 ||
            config.Tools.Any(t => t is null) || config.Zones.Any(z => z is null) || config.Skills.Any(s => s is null) ||
            config.Tools.Where(t => !draft || t.ItemId != Guid.Empty).Select(t => t.ItemId).Distinct().Count() !=
                config.Tools.Count(t => !draft || t.ItemId != Guid.Empty) ||
            config.Zones.Select(z => z.Id).Distinct().Count() != config.Zones.Count ||
            config.Skills.Select(s => s.Skill).Distinct().Count() != config.Skills.Count)
            throw Invalid();
        ValidateSounds(config.Sounds);
        foreach (var skill in config.Skills)
            if (!Enum.IsDefined(skill.Skill) || skill.MinimumLevel is < 1 or > 500 ||
                skill.CooldownMs is < 1000 or > 600000 || skill.DurationMs is < 0 or > 30000 ||
                !double.IsFinite(skill.Amount) || skill.Amount is < 0 or > 100 ||
                !double.IsFinite(skill.TensionCost) || skill.TensionCost is < 0 or > 100)
                throw Invalid();
        foreach (var tool in config.Tools)
        {
            if ((!draft && tool.ItemId == Guid.Empty) || !Enum.IsDefined(tool.Type) || tool.BaitQuantity is < 1 or > 100 ||
                (tool.BaitItemId != Guid.Empty && tool.BaitItemId == tool.ItemId) || tool.AnimationIntervalMs is < 250 or > 30000 ||
                !double.IsFinite(tool.Power) || tool.Power is < .25 or > 5) throw Invalid();
            ValidateSounds(tool.Sounds);
            tool.WorldProfile?.Validate();
        }
        var tools = config.Tools.Select(t => t.ItemId).ToHashSet();
        foreach (var zone in config.Zones)
        {
            if (zone.Id == Guid.Empty || string.IsNullOrWhiteSpace(zone.Name) || zone.Name.Length > 96 ||
                zone.LocalizedNames is null || zone.LocalizedNames.Count > 3 ||
                zone.LocalizedNames.Any(p => p.Key is not ("en" or "fr" or "es") || p.Value is null || p.Value.Length > 96) ||
                zone.MapIds is null || zone.MapIds.Count > MaximumMapsPerZone || zone.MapIds.Any(id => id == Guid.Empty) ||
                zone.MapIds.Distinct().Count() != zone.MapIds.Count ||
                zone.X is < 0 or > 10000 || zone.Y is < 0 or > 10000 || zone.Radius is < 0 or > 64 ||
                zone.MinimumWaitMs is < 1000 or > 120000 || zone.MaximumWaitMs < zone.MinimumWaitMs || zone.MaximumWaitMs > 120000 ||
                !double.IsFinite(zone.AutomaticFailureChance) || zone.AutomaticFailureChance is < 0 or > 1 ||
                !double.IsFinite(zone.AutomaticExperienceMultiplier) || zone.AutomaticExperienceMultiplier is < 0 or > 1 ||
                zone.AutomaticCatchMs is < 5000 or > 600000 || zone.ActiveLimitMs is < 10000 or > 180000 ||
                zone.LegendaryPromptMs is < 5000 or > 120000 || zone.Assets is null ||
                !double.IsFinite(zone.LegendaryChance) || zone.LegendaryChance is < 0 or > 1 ||
                !SafeAssetPath(zone.Assets.Background, ".png", ".jpg") || !SafeAssetPath(zone.Assets.Foreground, ".png", ".jpg") ||
                !SafeAssetPath(zone.Assets.Decorations, ".png", ".jpg") ||
                zone.Catches is null || zone.Catches.Count > 256 || zone.Catches.Any(c => c is null) ||
                zone.Catches.Select(c => c.Id).Distinct().Count() != zone.Catches.Count ||
                (!draft && zone.Enabled && (zone.MapIds.Count == 0 || zone.Catches.Count == 0))) throw Invalid();
            ValidateSounds(zone.Sounds);
            foreach (var entry in zone.Catches)
            {
                if (entry.Id == Guid.Empty || (!draft && entry.ItemId == Guid.Empty) || entry.ToolItemIds is null ||
                    entry.ToolItemIds.Count > 128 || (!draft && entry.ToolItemIds.Count == 0) ||
                    entry.ToolItemIds.Any(t => t == Guid.Empty || (!draft && !tools.Contains(t))) ||
                    entry.ToolItemIds.Distinct().Count() != entry.ToolItemIds.Count ||
                    !double.IsFinite(entry.Weight) || entry.Weight is <= 0 or > 1000000 ||
                    entry.MinimumLevel is < 1 or > 500 || entry.MinimumQuantity is < 1 or > 1000 ||
                    entry.MaximumQuantity < entry.MinimumQuantity || entry.MaximumQuantity > 1000 ||
                    entry.Experience is < 0 or > 1000000 || entry.Difficulty is < 1 or > 10 ||
                    entry.UtcStartHour is < 0 or > 23 || entry.UtcEndHour is < 0 or > 24 || entry.UtcStartHour == entry.UtcEndHour ||
                    entry.MinimumDepth is < 0 or > 100 || entry.MaximumDepth < entry.MinimumDepth || entry.MaximumDepth > 100 ||
                    entry.ChallengeSteps is < 1 or > 8 || entry.ChallengeWindowMs is < 800 or > 10000 ||
                    !SafeAssetPath(entry.Sprite, ".png") || entry.SpriteX is < 0 or > 16384 || entry.SpriteY is < 0 or > 16384 ||
                    entry.SpriteWidth is < 1 or > 2048 || entry.SpriteHeight is < 1 or > 2048) throw Invalid();
                ValidateSounds(entry.Sounds);
            }
        }
    }

    private static InvalidDataException Invalid() => new("Invalid fishing configuration.");

    private static void ValidateSounds(FishingSounds sounds)
    {
        if (sounds?.Cues is null || sounds.Cues.Count > 128 || sounds.Cues.Any(p =>
                !FishingSoundCatalog.Keys.Contains(p.Key) || p.Value is null ||
                !SafeAssetPath(p.Value.File, ".wav", ".ogg") || p.Value.Volume is < 0 or > 100 ||
                p.Value.CooldownMs is < 0 or > 600000 || p.Value.MaximumInstances is < 1 or > 16 ||
                p.Value.Range is < 0 or > 100)) throw Invalid();
    }

    public static bool WithinZone(FishingZoneDefinition zone, Guid map, double x, double y) =>
        zone.Enabled && map != Guid.Empty && zone.MapIds?.Contains(map) == true &&
        double.IsFinite(x) && double.IsFinite(y) && x >= 0 && y >= 0 &&
        (!zone.UseTileRadius || Math.Abs(x - zone.X) <= zone.Radius && Math.Abs(y - zone.Y) <= zone.Radius);

    public static bool DuringHours(int start, int end, int utcHour) => utcHour is >= 0 and < 24 &&
        (start < end ? utcHour >= start && utcHour < end : utcHour >= start || utcHour < end);

    /// <summary>A roll in [0,1) decides whether an ordinary automatic catch succeeds.</summary>
    public static bool AutomaticCatchSucceeds(FishingZoneDefinition zone, double roll) =>
        double.IsFinite(roll) && roll >= 0 && roll < 1 && roll >= zone.AutomaticFailureChance;

    /// <summary>Automatic fishing grants a configurable, rounded-down fraction of the normal base XP.</summary>
    public static int AutomaticExperience(FishingZoneDefinition zone, int baseExperience) =>
        (int)Math.Floor(Math.Max(0, baseExperience) * zone.AutomaticExperienceMultiplier);

    public static bool ShouldAutoCast(FishingState state, long now, long nextCastAt) =>
        state.Automatic && state.Phase is FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed && now >= nextCastAt;

    public static FishingCatchDefinition[] Eligible(FishingZoneDefinition zone, Guid equippedToolId,
        int level, int utcHour, double depth, bool automatic, Func<Guid, long> variable) =>
        zone.Catches.Where(entry => entry.ToolItemIds.Contains(equippedToolId) && level >= entry.MinimumLevel &&
            DuringHours(entry.UtcStartHour, entry.UtcEndHour, utcHour) && depth >= entry.MinimumDepth && depth <= entry.MaximumDepth &&
            (!automatic || !entry.Legendary || zone.LegendaryDuringAutomatic) &&
            (entry.RequiredVariableId == Guid.Empty || variable(entry.RequiredVariableId) >= entry.RequiredVariableValue)).ToArray();

    public static FishingCatchDefinition? Select(IReadOnlyList<FishingCatchDefinition> eligible, double unitRoll)
    {
        if (!double.IsFinite(unitRoll) || unitRoll is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(unitRoll));
        if (eligible.Count == 0) return null;
        var roll = unitRoll * eligible.Sum(entry => entry.Weight);
        foreach (var entry in eligible)
        {
            roll -= entry.Weight;
            if (roll < 0) return entry;
        }
        return eligible[^1];
    }

    public static FishingCatchDefinition? SelectEncounter(IReadOnlyList<FishingCatchDefinition> eligible,
        double legendaryChance, double legendaryRoll, double weightRoll)
    {
        if (!double.IsFinite(legendaryChance) || legendaryChance is < 0 or > 1 ||
            !double.IsFinite(legendaryRoll) || legendaryRoll is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(legendaryChance));
        var legendary = eligible.Where(c => c.Legendary).ToArray();
        // An empty normal table never turns a rare encounter into a guaranteed legendary.
        return Select(legendary.Length > 0 && legendaryRoll < legendaryChance ? legendary :
            eligible.Where(c => !c.Legendary).ToArray(), weightRoll);
    }
}
