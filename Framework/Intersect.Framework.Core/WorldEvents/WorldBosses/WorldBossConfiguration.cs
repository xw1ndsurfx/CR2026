using System.Text.Json;

namespace Intersect.Framework.Core.WorldEvents.WorldBosses;

[Flags]
public enum WorldBossScheduleDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday,
}

/// <summary>A recurring, overworld-only boss encounter configured from Events in the editor.</summary>
public sealed class WorldBossDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New World Boss";
    public bool Enabled { get; set; }
    public Guid NpcId { get; set; }
    public Guid MapId { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public WorldBossScheduleDays ScheduleDays { get; set; } = WorldBossScheduleDays.All;
    public int StartHour { get; set; } = 20;
    public int StartMinute { get; set; }
    public int LifetimeMinutes { get; set; } = 60;

    public bool Reminder60Enabled { get; set; } = true;
    public bool Reminder30Enabled { get; set; } = true;
    public bool Reminder15Enabled { get; set; } = true;
    public bool Reminder5Enabled { get; set; } = true;
    public string ReminderMessage { get; set; } =
        "{name} will appear in {remaining} at {map} ({x}, {y}).";
    public string SpawnMessage { get; set; } =
        "{name} has appeared at {map} ({x}, {y})!";
    public string DefeatedMessage { get; set; } = "{name} has been defeated!";
    public string ExpiredMessage { get; set; } = "{name} has disappeared from {map}.";
    public string AnnouncementSound { get; set; } = string.Empty;

    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 96 &&
        NpcId != Guid.Empty && MapId != Guid.Empty &&
        SpawnX is >= 0 and <= 255 && SpawnY is >= 0 and <= 255 &&
        ScheduleDays != WorldBossScheduleDays.None &&
        (ScheduleDays & ~WorldBossScheduleDays.All) == WorldBossScheduleDays.None &&
        StartHour is >= 0 and <= 23 && StartMinute is >= 0 and <= 59 &&
        LifetimeMinutes is >= 1 and <= 1_440 &&
        ReminderMessage is { Length: <= 512 } &&
        SpawnMessage is { Length: <= 512 } &&
        DefeatedMessage is { Length: <= 512 } &&
        ExpiredMessage is { Length: <= 512 } &&
        AnnouncementSound is { Length: <= 260 };

    public bool RunsOn(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => ScheduleDays.HasFlag(WorldBossScheduleDays.Sunday),
        DayOfWeek.Monday => ScheduleDays.HasFlag(WorldBossScheduleDays.Monday),
        DayOfWeek.Tuesday => ScheduleDays.HasFlag(WorldBossScheduleDays.Tuesday),
        DayOfWeek.Wednesday => ScheduleDays.HasFlag(WorldBossScheduleDays.Wednesday),
        DayOfWeek.Thursday => ScheduleDays.HasFlag(WorldBossScheduleDays.Thursday),
        DayOfWeek.Friday => ScheduleDays.HasFlag(WorldBossScheduleDays.Friday),
        DayOfWeek.Saturday => ScheduleDays.HasFlag(WorldBossScheduleDays.Saturday),
        _ => false,
    };
}

public sealed class WorldBossConfiguration
{
    public static WorldBossConfiguration Instance { get; private set; } = new();

    public WorldBossDefinition[] Bosses { get; set; } = [];

    public bool IsStructurallyValid =>
        Bosses is { Length: <= 128 } &&
        Bosses.All(boss => boss is { IsStructurallyValid: true }) &&
        Bosses.Select(boss => boss.Id).Distinct().Count() == Bosses.Length;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static WorldBossConfiguration FromJson(string? json)
    {
        var configuration = string.IsNullOrWhiteSpace(json)
            ? new WorldBossConfiguration()
            : JsonSerializer.Deserialize<WorldBossConfiguration>(json, JsonOptions) ??
              new WorldBossConfiguration();

        configuration.Bosses ??= [];
        foreach (var boss in configuration.Bosses)
        {
            if (boss == null)
                throw new InvalidDataException("Invalid world boss definition.");

            boss.Name ??= string.Empty;
            boss.ReminderMessage ??= string.Empty;
            boss.SpawnMessage ??= string.Empty;
            boss.DefeatedMessage ??= string.Empty;
            boss.ExpiredMessage ??= string.Empty;
            boss.AnnouncementSound ??= string.Empty;
        }

        if (!configuration.IsStructurallyValid)
            throw new InvalidDataException("Invalid world boss configuration.");

        return configuration;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
