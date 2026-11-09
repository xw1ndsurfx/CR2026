using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Fishing;

public enum FishingToolType { Rod, Cage, Net }
public enum FishingPhase { Ready, Waiting, Fighting, LegendaryPrompt, LegendaryChallenge, Caught, Failed, Closed, Retrieving, Loot }
public enum FishingSkillType { Stabilize, Pull, Repair, Dive }

public sealed class FishingSkillDefinition
{
    public FishingSkillType Skill { get; set; }
    public bool Enabled { get; set; } = true;
    public int MinimumLevel { get; set; } = 1;
    public int CooldownMs { get; set; } = 12000;
    public int DurationMs { get; set; } = 4000;
    public double Amount { get; set; } = 25;
    public double TensionCost { get; set; }
    public static List<FishingSkillDefinition> Defaults() =>
    [
        new() { Skill = FishingSkillType.Stabilize, Amount = 50 },
        new() { Skill = FishingSkillType.Pull, MinimumLevel = 3, Amount = 12, TensionCost = 15 },
        new() { Skill = FishingSkillType.Repair, MinimumLevel = 5, CooldownMs = 20000, Amount = 30 },
        new() { Skill = FishingSkillType.Dive, CooldownMs = 6000, Amount = 20 },
    ];
}

public sealed class FishingAssets
{
    public string Background { get; set; } = "fishing/backgrounds/Water_Background4.png";
    public string Foreground { get; set; } = "fishing/backgrounds/Water_Background5.png";
    public string Decorations { get; set; } = "fishing/gui/coral_foreground.png";
}

public sealed class FishingSoundCue
{
    public string File { get; set; } = "";
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; }
    public int CooldownMs { get; set; } = 100;
    public int MaximumInstances { get; set; } = 2;
    public bool Spatial { get; set; }
    public int Range { get; set; } = 8;
}

public sealed class FishingSounds
{
    public Dictionary<string, FishingSoundCue> Cues { get; set; } = new(StringComparer.Ordinal);
}

public sealed class FishingToolDefinition
{
    public Guid ItemId { get; set; }
    public FishingWorldProfile WorldProfile { get; set; } = new();
    public FishingSounds Sounds { get; set; } = new();
    public FishingToolType Type { get; set; }
    public Guid BaitItemId { get; set; }
    public int BaitQuantity { get; set; } = 1;
    public Guid CastAnimationId { get; set; }
    public Guid UseAnimationId { get; set; }
    public Guid CatchAnimationId { get; set; }
    public int AnimationIntervalMs { get; set; } = 1600;
    public double Power { get; set; } = 1;
}

public sealed class FishingLootDefinition
{
    public Guid ItemId { get; set; }
    public int MinimumQuantity { get; set; } = 1;
    public int MaximumQuantity { get; set; } = 1;
    public double Chance { get; set; } = 1;
}

public sealed class FishingCatchDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FishingSounds Sounds { get; set; } = new();
    public Guid ItemId { get; set; }
    public List<Guid> ToolItemIds { get; set; } = [];
    public List<FishingLootDefinition> BonusLoot { get; set; } = [];
    public double Weight { get; set; } = 1;
    public int MinimumLevel { get; set; } = 1;
    public int MinimumQuantity { get; set; } = 1;
    public int MaximumQuantity { get; set; } = 1;
    public int Experience { get; set; } = 25;
    public int Difficulty { get; set; } = 3;
    public int UtcStartHour { get; set; }
    public int UtcEndHour { get; set; } = 24;
    public int MinimumDepth { get; set; }
    public int MaximumDepth { get; set; } = 100;
    public Guid RequiredVariableId { get; set; }
    public long RequiredVariableValue { get; set; } = 1;
    public bool Legendary { get; set; }
    public int ChallengeSteps { get; set; } = 3;
    public int ChallengeWindowMs { get; set; } = 2500;
    public string Sprite { get; set; } = "";
    public int SpriteX { get; set; }
    public int SpriteY { get; set; }
    public int SpriteWidth { get; set; } = 32;
    public int SpriteHeight { get; set; } = 32;
}

public sealed class FishingZoneDefinition : IJsonOnDeserialized
{
    private List<Guid> _mapIds = [];
    private bool _mapIdsSpecified;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Dictionary<string, string> LocalizedNames { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public List<Guid> MapIds
    {
        get => _mapIds;
        set { _mapIds = value; _mapIdsSpecified = true; }
    }
    public bool UseTileRadius { get; set; }
    /// <summary>Legacy JSON input. Deserialization migrates it without widening the original tile area.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Radius { get; set; } = 3;
    public bool AllowAutomatic { get; set; } = true;
    public bool LegendaryDuringAutomatic { get; set; } = true;
    public double LegendaryChance { get; set; } = .02;
    public int MinimumWaitMs { get; set; } = 3500;
    public int MaximumWaitMs { get; set; } = 7000;
    /// <summary>Probability that an ordinary automatic catch escapes (0 to 1). Missing legacy JSON uses 50%.</summary>
    public double AutomaticFailureChance { get; set; } = .50;
    /// <summary>Share of ordinary catch XP awarded for successful automatic fishing (0 to 1). Missing legacy JSON uses 20%.</summary>
    public double AutomaticExperienceMultiplier { get; set; } = .20;
    public int AutomaticCatchMs { get; set; } = 15000;
    public int ActiveLimitMs { get; set; } = 60000;
    public int LegendaryPromptMs { get; set; } = 30000;
    public FishingAssets Assets { get; set; } = new();
    public FishingSounds Sounds { get; set; } = new();
    public List<FishingCatchDefinition> Catches { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!_mapIdsSpecified)
        {
            _mapIds = MapId == Guid.Empty ? [] : [MapId];
            UseTileRadius = true;
        }
        // An explicit empty or null MapIds value must never resurrect the legacy map.
        // Validation rejects null; an enabled zone needs a selection before it can be applied.
        MapId = Guid.Empty;
        _mapIdsSpecified = true;
    }
}

public sealed class FishingConfiguration
{
    public int Version { get; set; } = 1;
    public FishingSounds Sounds { get; set; } = new();
    public Guid ProfessionId { get; set; }
    public List<FishingSkillDefinition> Skills { get; set; } = FishingSkillDefinition.Defaults();
    public List<FishingToolDefinition> Tools { get; set; } = [];
    public List<FishingZoneDefinition> Zones { get; set; } = [];
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static FishingConfiguration FromJson(string json) =>
        JsonSerializer.Deserialize<FishingConfiguration>(json, JsonOptions) ?? throw new InvalidDataException("Invalid fishing configuration.");
}

public sealed class FishingInventoryEntry
{
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
}

public sealed class FishingState
{
    public Guid SessionId { get; set; }
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = "";
    public FishingPhase Phase { get; set; }
    public bool Automatic { get; set; }
    public bool AutomaticResumePending { get; set; }
    public bool Reeling { get; set; }
    public bool CanBankLoot { get; set; }
    public bool BankLoot { get; set; }
    public bool AllowAutomatic { get; set; }
    public Guid ToolItemId { get; set; }
    public Guid BaitItemId { get; set; }
    public int RemainingBait { get; set; }
    public FishingToolType ToolType { get; set; }
    public Guid CatchItemId { get; set; }
    public Guid CatchDefinitionId { get; set; }
    public int CatchQuantity { get; set; }
    public bool Legendary { get; set; }
    public double Tension { get; set; }
    public double FishStamina { get; set; } = 100;
    public double Progress { get; set; }
    public double LineCondition { get; set; } = 100;
    public double Depth { get; set; } = 25;
    public bool Resisting { get; set; }
    public int[] SkillCooldownsMs { get; set; } = new int[4];
    public bool[] SkillAvailable { get; set; } = new bool[4];
    public int RemainingMs { get; set; }
    public int ChallengeExpectedInput { get; set; } = -1;
    public int ChallengeStep { get; set; }
    public int ChallengeSteps { get; set; }
    public int Level { get; set; } = 1;
    public int MaximumLevel { get; set; } = 25;
    public long Experience { get; set; }
    public long ExperienceIntoLevel { get; set; }
    public long ExperienceRequired { get; set; }
    public int ExperienceGained { get; set; }
    public int CatchesThisSession { get; set; }
    public long Revision { get; set; }
    public string Message { get; set; } = "";
    public string Sound { get; set; } = "";
    public string SoundCue { get; set; } = "";
    public long SoundSequence { get; set; }
    public List<FishingInventoryEntry> Inventory { get; set; } = [];
    public List<FishingInventoryEntry> Loot { get; set; } = [];
    public static FishingState? FromJson(string json) => JsonSerializer.Deserialize<FishingState>(json, FishingConfiguration.JsonOptions);
    public string ToJson() => JsonSerializer.Serialize(this, FishingConfiguration.JsonOptions);
}
