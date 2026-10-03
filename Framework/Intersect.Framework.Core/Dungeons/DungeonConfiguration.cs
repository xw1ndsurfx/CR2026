using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Dungeons;

public enum DungeonRank
{
    F = 0,
    E = 1,
    D = 2,
    C = 3,
    B = 4,
    A = 5,
    S = 6,
}

public enum DungeonAvailabilityMode
{
    Always = 0,
    Manual = 1,
    Scheduled = 2,
}

[Flags]
public enum DungeonWeekdays
{
    None = 0,
    Monday = 1 << 0,
    Tuesday = 1 << 1,
    Wednesday = 1 << 2,
    Thursday = 1 << 3,
    Friday = 1 << 4,
    Saturday = 1 << 5,
    Sunday = 1 << 6,
    EveryDay = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}

public sealed class DungeonDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Dungeon";
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Guid AssociatedQuestId { get; set; }
    public DungeonRank Rank { get; set; } = DungeonRank.F;
    public int MinimumLevel { get; set; } = 1;
    public int RecommendedLevel { get; set; } = 1;
    public int MaximumLevel { get; set; }
    public int MinimumPartySize { get; set; } = 1;
    public int MaximumPartySize { get; set; } = 5;
    public int TimeLimitMinutes { get; set; }
    public bool PremiumRequired { get; set; }

    // Completion / boss
    public Guid FinalBossNpcId { get; set; }
    public long CompletionExperience { get; set; }
    public Guid CompletionItemId { get; set; }
    public int CompletionItemQuantity { get; set; }
    public Guid CompletionCommonEventId { get; set; }
    public Guid FailureCommonEventId { get; set; }

    public DungeonAvailabilityMode AvailabilityMode { get; set; } = DungeonAvailabilityMode.Always;
    public bool ManualAvailable { get; set; } = true;
    public DungeonWeekdays AvailableDays { get; set; } = DungeonWeekdays.EveryDay;
    public int StartMinuteOfDay { get; set; }
    public int EndMinuteOfDay { get; set; } = 1440;
    public int SortOrder { get; set; }

    [JsonIgnore]
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 128 &&
        (Description?.Length ?? 0) <= 2_000 &&
        (Image?.Length ?? 0) <= 255 &&
        (Location?.Length ?? 0) <= 255 &&
        MinimumLevel is >= 1 and <= 1_000_000 &&
        RecommendedLevel is >= 1 and <= 1_000_000 &&
        MaximumLevel is >= 0 and <= 1_000_000 &&
        (MaximumLevel == 0 || MaximumLevel >= MinimumLevel) &&
        MinimumPartySize is >= 1 and <= 100 &&
        MaximumPartySize is >= 1 and <= 100 &&
        MaximumPartySize >= MinimumPartySize &&
        TimeLimitMinutes is >= 0 and <= 24 * 60 &&
        CompletionExperience is >= 0 and <= 2_000_000_000 &&
        CompletionItemQuantity is >= 0 and <= 1_000_000_000 &&
        StartMinuteOfDay is >= 0 and <= 1439 &&
        EndMinuteOfDay is >= 0 and <= 1440 &&
        (AvailabilityMode != DungeonAvailabilityMode.Scheduled || AvailableDays != DungeonWeekdays.None);
}

public sealed class DungeonConfiguration
{
    public static DungeonConfiguration Instance { get; private set; } = new();

    public DungeonDefinition[] Dungeons { get; set; } = [];

    [JsonIgnore]
    public bool IsStructurallyValid =>
        (Dungeons ?? []).Length <= 2_048 &&
        (Dungeons ?? []).All(dungeon => dungeon is { IsStructurallyValid: true }) &&
        (Dungeons ?? []).Select(dungeon => dungeon.Id).Distinct().Count() == (Dungeons ?? []).Length;

    public DungeonDefinition? Find(Guid id) =>
        (Dungeons ?? []).FirstOrDefault(dungeon => dungeon.Id == id);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static DungeonConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new DungeonConfiguration()
            : JsonSerializer.Deserialize<DungeonConfiguration>(json, JsonOptions) ?? new DungeonConfiguration();

        value.Dungeons ??= [];
        foreach (var dungeon in value.Dungeons)
        {
            dungeon.Name ??= "Dungeon";
            dungeon.Description ??= string.Empty;
            dungeon.Image ??= string.Empty;
            dungeon.Location ??= string.Empty;
        }

        if (!value.IsStructurallyValid)
            throw new InvalidDataException("Invalid dungeon configuration.");

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
