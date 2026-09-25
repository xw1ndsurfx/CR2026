using System.Text.Json;

namespace Intersect.Framework.Core.WorldEvents.Invasions;

[Flags]
public enum InvasionScheduleDays
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

public sealed class InvasionSpawnDefinition
{
    public Guid NpcId { get; set; }
    public Guid SpawnMapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Count { get; set; } = 1;
    public bool IsBoss { get; set; }
    public int ObjectiveDamage { get; set; } = 5;

    public bool IsStructurallyValid =>
        NpcId != Guid.Empty &&
        SpawnMapId != Guid.Empty &&
        X is >= 0 and <= 255 &&
        Y is >= 0 and <= 255 &&
        Count is >= 1 and <= 500 &&
        ObjectiveDamage is >= 1 and <= 1_000_000;
}

public sealed class InvasionWaveDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Wave";
    public int DelaySeconds { get; set; } = 10;
    public InvasionSpawnDefinition[] Spawns { get; set; } = [];

    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 64 &&
        DelaySeconds is >= 0 and <= 86_400 &&
        Spawns is { Length: > 0 and <= 64 } &&
        Spawns.All(spawn => spawn is { IsStructurallyValid: true });
}

public sealed class InvasionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Invasion";
    public bool Enabled { get; set; }
    public InvasionScheduleDays ScheduleDays { get; set; } = InvasionScheduleDays.All;
    public int StartHour { get; set; } = 20;
    public int StartMinute { get; set; }

    public Guid TargetMapId { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TargetHealth { get; set; } = 2_500;
    public int ObjectiveHitIntervalMs { get; set; } = 1_000;

    public long RewardExperience { get; set; } = 500;
    public InvasionWaveDefinition[] Waves { get; set; } = [];

    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 96 &&
        ScheduleDays != InvasionScheduleDays.None &&
        StartHour is >= 0 and <= 23 &&
        StartMinute is >= 0 and <= 59 &&
        TargetMapId != Guid.Empty &&
        TargetX is >= 0 and <= 255 &&
        TargetY is >= 0 and <= 255 &&
        TargetHealth is >= 1 and <= 1_000_000_000 &&
        ObjectiveHitIntervalMs is >= 250 and <= 60_000 &&
        RewardExperience is >= 0 and <= 2_000_000_000 &&
        Waves is { Length: > 0 and <= 128 } &&
        Waves.All(wave => wave is { IsStructurallyValid: true }) &&
        Waves.Select(wave => wave.Id).Distinct().Count() == Waves.Length;

    public bool RunsOn(DayOfWeek day)
    {
        var flag = day switch
        {
            DayOfWeek.Sunday => InvasionScheduleDays.Sunday,
            DayOfWeek.Monday => InvasionScheduleDays.Monday,
            DayOfWeek.Tuesday => InvasionScheduleDays.Tuesday,
            DayOfWeek.Wednesday => InvasionScheduleDays.Wednesday,
            DayOfWeek.Thursday => InvasionScheduleDays.Thursday,
            DayOfWeek.Friday => InvasionScheduleDays.Friday,
            DayOfWeek.Saturday => InvasionScheduleDays.Saturday,
            _ => InvasionScheduleDays.None,
        };
        return (ScheduleDays & flag) != 0;
    }
}

public sealed class InvasionConfiguration
{
    public static InvasionConfiguration Instance { get; private set; } = new();

    public InvasionDefinition[] Invasions { get; set; } = [];

    public bool IsStructurallyValid =>
        Invasions is { Length: <= 128 } &&
        Invasions.All(invasion => invasion is { IsStructurallyValid: true }) &&
        Invasions.Select(invasion => invasion.Id).Distinct().Count() == Invasions.Length;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static InvasionConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new InvasionConfiguration()
            : JsonSerializer.Deserialize<InvasionConfiguration>(json, JsonOptions) ?? new InvasionConfiguration();

        value.Invasions ??= [];
        foreach (var invasion in value.Invasions)
        {
            invasion.Waves ??= [];
            foreach (var wave in invasion.Waves)
                wave.Spawns ??= [];
        }

        if (!value.IsStructurallyValid)
            throw new InvalidDataException("Invalid invasion configuration.");

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
