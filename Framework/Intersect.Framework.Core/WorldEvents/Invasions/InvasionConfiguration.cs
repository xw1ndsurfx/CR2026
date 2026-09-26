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
    public Guid TargetEventId { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TargetHealth { get; set; } = 2_500;
    public int ObjectiveHitIntervalMs { get; set; } = 1_000;

    public string InvasionMusic { get; set; } = string.Empty;
    public int NightBrightness { get; set; } = 20;
    public int OverlayAlpha { get; set; } = 120;
    public int OverlayRed { get; set; } = 50;
    public int OverlayGreen { get; set; } = 255;
    public int OverlayBlue { get; set; } = 50;
    public string Fog { get; set; } = "brume2.png";
    public int FogAlpha { get; set; } = 150;
    public int FogXSpeed { get; set; } = 3;
    public int FogYSpeed { get; set; } = -3;
    public bool EnvironmentOutdoorsOnly { get; set; } = true;

    public bool ScaleNpcToPlayers { get; set; } = true;
    public int ScalingMinimumLevel { get; set; } = 1;
    public int ScalingMaximumLevel { get; set; } = 100;
    public int ScalingLevelOffset { get; set; }
    public int ExtraPlayerHealthPercent { get; set; } = 25;
    public int BossHealthPercent { get; set; } = 250;
    public int BossDamagePercent { get; set; } = 125;

    // RewardExperience is the baseline reward for a defender whose contribution
    // matches the average contribution of all defenders in the invasion.
    public int ParticipationMinimumRewardPercent { get; set; } = 10;
    public int ParticipationMaximumRewardPercent { get; set; } = 200;
    public int HealingContributionPercent { get; set; } = 100;

    public bool Reminder60Enabled { get; set; } = true;
    public string Reminder60Message { get; set; } = "{name} will begin in 1 hour near {island}.";
    public string Reminder60Sound { get; set; } = string.Empty;
    public bool Reminder30Enabled { get; set; } = true;
    public string Reminder30Message { get; set; } = "{name} will begin in 30 minutes near {island}.";
    public string Reminder30Sound { get; set; } = string.Empty;
    public bool Reminder15Enabled { get; set; } = true;
    public string Reminder15Message { get; set; } = "{name} will begin in 15 minutes near {island}.";
    public string Reminder15Sound { get; set; } = string.Empty;
    public bool Reminder5Enabled { get; set; } = true;
    public string Reminder5Message { get; set; } = "{name} will begin in 5 minutes near {island}.";
    public string Reminder5Sound { get; set; } = string.Empty;

    public bool PreStartCinematicEnabled { get; set; }
    public Guid PreStartCinematicEventId { get; set; }
    public int PreStartCinematicLeadSeconds { get; set; } = 15;

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
        NightBrightness is >= 0 and <= 100 &&
        OverlayAlpha is >= 0 and <= 255 &&
        OverlayRed is >= 0 and <= 255 &&
        OverlayGreen is >= 0 and <= 255 &&
        OverlayBlue is >= 0 and <= 255 &&
        FogAlpha is >= 0 and <= 255 &&
        FogXSpeed is >= -5 and <= 5 &&
        FogYSpeed is >= -5 and <= 5 &&
        ScalingMinimumLevel is >= 1 and <= 1_000 &&
        ScalingMaximumLevel is >= 1 and <= 1_000 &&
        ScalingMinimumLevel <= ScalingMaximumLevel &&
        ScalingLevelOffset is >= -1_000 and <= 1_000 &&
        ExtraPlayerHealthPercent is >= 0 and <= 500 &&
        BossHealthPercent is >= 1 and <= 2_000 &&
        BossDamagePercent is >= 1 and <= 1_000 &&
        ParticipationMinimumRewardPercent is >= 0 and <= 500 &&
        ParticipationMaximumRewardPercent is >= 1 and <= 500 &&
        ParticipationMinimumRewardPercent <= ParticipationMaximumRewardPercent &&
        HealingContributionPercent is >= 0 and <= 500 &&
        Reminder60Message.Length <= 512 &&
        Reminder60Sound.Length <= 260 &&
        Reminder30Message.Length <= 512 &&
        Reminder30Sound.Length <= 260 &&
        Reminder15Message.Length <= 512 &&
        Reminder15Sound.Length <= 260 &&
        Reminder5Message.Length <= 512 &&
        Reminder5Sound.Length <= 260 &&
        PreStartCinematicLeadSeconds is >= 1 and <= 300 &&
        (!PreStartCinematicEnabled || PreStartCinematicEventId != Guid.Empty) &&
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
            invasion.Reminder60Message ??= string.Empty;
            invasion.Reminder60Sound ??= string.Empty;
            invasion.Reminder30Message ??= string.Empty;
            invasion.Reminder30Sound ??= string.Empty;
            invasion.Reminder15Message ??= string.Empty;
            invasion.Reminder15Sound ??= string.Empty;
            invasion.Reminder5Message ??= string.Empty;
            invasion.Reminder5Sound ??= string.Empty;
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
