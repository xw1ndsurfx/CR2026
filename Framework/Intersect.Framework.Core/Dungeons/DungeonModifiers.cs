using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Dungeons;

public enum DungeonModifierMode { Fixed, Weekly }

public sealed class DungeonModifier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int EnemyHealthPercent { get; set; } = 100;
    public int EnemyDamagePercent { get; set; } = 100;
    public int TimeLimitPercent { get; set; } = 100;
    public int LivesPenalty { get; set; }
    public int CompletionExperiencePercent { get; set; } = 100;
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 80 &&
        EnemyHealthPercent is >= 100 and <= 500 && EnemyDamagePercent is >= 100 and <= 300 &&
        TimeLimitPercent is >= 25 and <= 100 && LivesPenalty is >= 0 and <= 98 && CompletionExperiencePercent is >= 100 and <= 300;
}

public sealed class DungeonModifierConfiguration
{
    public bool Enabled { get; set; }
    public DungeonModifierMode Mode { get; set; }
    public int ActiveCount { get; set; } = 1;
    public DungeonModifier[] Pool { get; set; } = [];
    [JsonIgnore] public bool IsValid => Enum.IsDefined(Mode) && ActiveCount is >= 1 and <= 3 && Pool is { Length: <= 12 } &&
        Pool.All(m => m is { IsValid: true }) && Pool.Select(m => m.Id).Distinct().Count() == Pool.Length &&
        (!Enabled || Pool.Length >= (Mode == DungeonModifierMode.Weekly ? ActiveCount : 1)) && (Mode != DungeonModifierMode.Fixed || Pool.Length <= 3);

    public DungeonModifierSnapshot Resolve(Guid dungeonId, DateTimeOffset now)
    {
        if (!Enabled || !IsValid) return new();
        var monday = now.UtcDateTime.Date.AddDays(-((int)now.UtcDateTime.DayOfWeek + 6) % 7);
        var pool = Mode == DungeonModifierMode.Weekly
            ? Pool.OrderBy(m => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{dungeonId:N}/{monday:yyyy-MM-dd}/{m.Id:N}"))), StringComparer.Ordinal).Take(ActiveCount)
            : Pool.AsEnumerable();
        var chosen = pool.ToArray();
        return new()
        {
            Names = chosen.Select(m => m.Name).ToArray(),
            HealthPercent = Math.Min(500, 100 + chosen.Sum(m => m.EnemyHealthPercent - 100)),
            DamagePercent = Math.Min(300, 100 + chosen.Sum(m => m.EnemyDamagePercent - 100)),
            TimePercent = Math.Max(25, 100 - chosen.Sum(m => 100 - m.TimeLimitPercent)),
            LivesPenalty = Math.Min(98, chosen.Sum(m => m.LivesPenalty)),
            ExperiencePercent = Math.Min(300, 100 + chosen.Sum(m => m.CompletionExperiencePercent - 100)),
        };
    }
}

public sealed class DungeonModifierSnapshot
{
    public string[] Names { get; init; } = [];
    public int HealthPercent { get; init; } = 100;
    public int DamagePercent { get; init; } = 100;
    public int TimePercent { get; init; } = 100;
    public int LivesPenalty { get; init; }
    public int ExperiencePercent { get; init; } = 100;
    public static long Scale(long value, int percent, long maximum = long.MaxValue) => value <= 0 ? value :
        (long)Math.Min(maximum, Math.Max(1m, decimal.Ceiling((decimal)value * percent / 100m)));
}
