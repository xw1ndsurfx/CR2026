using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Achievements;

public enum AchievementObjectiveType
{
    PlayerLevel = 0,
    NpcKills = 1,
    QuestCompletions = 2,
    ResourceHarvests = 3,
    ItemsObtained = 4,
    ProfessionLevel = 5,
    MiniGameWins = 6,
    CustomCounter = 7,
    LockpickingSuccesses = 8,
    LockpickingPerfects = 9,
    LockpickingDifficultyFive = 10,
    LockpickingBrokenPicks = 11,
    LockpickingFastPicks = 12,
    LockpickingNoBreakPicks = 13,
}

public sealed class AchievementRewardDefinition
{
    public Guid ItemId { get; set; } = Guid.Empty;
    public int ItemQuantity { get; set; }
    public long Experience { get; set; }
    public Guid CurrencyItemId { get; set; } = Guid.Empty;
    public int CurrencyQuantity { get; set; }
    public Guid CommonEventId { get; set; } = Guid.Empty;

    [JsonIgnore]
    public bool IsStructurallyValid =>
        ItemQuantity is >= 0 and <= 1_000_000_000 &&
        CurrencyQuantity is >= 0 and <= 1_000_000_000 &&
        Experience is >= 0 and <= 2_000_000_000 &&
        (ItemQuantity == 0 || ItemId != Guid.Empty) &&
        (CurrencyQuantity == 0 || CurrencyItemId != Guid.Empty);
}

public sealed class AchievementDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Achievement";
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Icon { get; set; } = string.Empty;
    public AchievementObjectiveType ObjectiveType { get; set; } = AchievementObjectiveType.CustomCounter;
    public Guid TargetId { get; set; } = Guid.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public long TargetAmount { get; set; } = 1;
    public bool HiddenUntilCompleted { get; set; }
    public int SortOrder { get; set; }
    public string SteamApiName { get; set; } = string.Empty;
    public AchievementRewardDefinition Reward { get; set; } = new();

    [JsonIgnore]
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 128 &&
        (Description?.Length ?? 0) <= 2_000 &&
        (Category?.Length ?? 0) <= 96 &&
        (Icon?.Length ?? 0) <= 255 &&
        (TargetKey?.Length ?? 0) <= 128 &&
        TargetAmount is >= 1 and <= 2_000_000_000 &&
        (SteamApiName?.Length ?? 0) <= 128 &&
        (Reward?.IsStructurallyValid ?? false) &&
        (ObjectiveType != AchievementObjectiveType.ProfessionLevel || TargetId != Guid.Empty);
}

public sealed class AchievementConfiguration
{
    public static AchievementConfiguration Instance { get; private set; } = new();

    public AchievementDefinition[] Achievements { get; set; } = [];

    [JsonIgnore]
    public bool IsStructurallyValid =>
        (Achievements ?? []).Length <= 2_048 &&
        (Achievements ?? []).All(a => a is { IsStructurallyValid: true }) &&
        (Achievements ?? []).Select(a => a.Id).Distinct().Count() == (Achievements ?? []).Length &&
        (Achievements ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.SteamApiName))
            .Select(a => a.SteamApiName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() ==
        (Achievements ?? []).Count(a => !string.IsNullOrWhiteSpace(a.SteamApiName));

    public AchievementDefinition? Find(Guid id) =>
        (Achievements ?? []).FirstOrDefault(a => a.Id == id);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AchievementConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new AchievementConfiguration()
            : JsonSerializer.Deserialize<AchievementConfiguration>(json, JsonOptions) ?? new AchievementConfiguration();

        value.Achievements ??= [];
        foreach (var achievement in value.Achievements)
        {
            achievement.Name ??= "Achievement";
            achievement.Description ??= string.Empty;
            achievement.Category ??= "General";
            achievement.Icon ??= string.Empty;
            achievement.TargetKey ??= string.Empty;
            achievement.SteamApiName ??= string.Empty;
            achievement.Reward ??= new AchievementRewardDefinition();
        }

        if (!value.IsStructurallyValid)
            throw new InvalidDataException("Invalid achievement configuration.");

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
