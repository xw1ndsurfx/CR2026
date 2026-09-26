using System.Text.Json;

namespace Intersect.Framework.Core.Professions;

public sealed record ProfessionResourceLink(Guid ResourceId, int RequiredLevel, long Experience)
{
    public bool IsValid(int maxLevel) =>
        ResourceId != Guid.Empty &&
        RequiredLevel >= 1 &&
        RequiredLevel <= maxLevel &&
        Experience is >= 0 and <= 2_000_000_000;
}

public sealed record ProfessionLevelReward(int Level, Guid ItemId, int Quantity, Guid EventId)
{
    public bool IsValid(int maxLevel) =>
        Level >= 2 &&
        Level <= maxLevel &&
        ((ItemId != Guid.Empty && Quantity is >= 1 and <= 1_000_000_000) || EventId != Guid.Empty);
}

public sealed class ProfessionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Profession";
    public string Description { get; set; } = string.Empty;
    public int MaximumLevel { get; set; } = 100;
    public long BaseExperience { get; set; } = 100;
    public double ExperienceGrowth { get; set; } = 1.12d;
    public ProfessionResourceLink[] Resources { get; set; } = [];
    public ProfessionLevelReward[] LevelRewards { get; set; } = [];

    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 96 &&
        Description.Length <= 2_000 &&
        MaximumLevel is >= 1 and <= 500 &&
        BaseExperience is >= 1 and <= 2_000_000_000 &&
        ExperienceGrowth is >= 1d and <= 10d &&
        (Resources ?? []).All(link => link is not null && link.IsValid(MaximumLevel)) &&
        (Resources ?? []).Select(link => link.ResourceId).Distinct().Count() == (Resources ?? []).Length &&
        (LevelRewards ?? []).All(reward => reward is not null && reward.IsValid(MaximumLevel));

    public long ExperienceForNextLevel(int level)
    {
        if (level < 1 || level >= MaximumLevel) return -1;
        var value = BaseExperience * Math.Pow(ExperienceGrowth, level - 1);
        return value >= long.MaxValue
            ? long.MaxValue
            : Math.Max(1L, (long)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    public long ExperienceToReachLevel(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        targetLevel = Math.Min(targetLevel, MaximumLevel);
        long total = 0;
        for (var level = 1; level < targetLevel; ++level)
        {
            var required = ExperienceForNextLevel(level);
            if (required < 0 || total > long.MaxValue - required) return long.MaxValue;
            total += required;
        }
        return total;
    }

    public int LevelForExperience(long totalExperience)
    {
        var level = 1;
        var remaining = Math.Max(0L, totalExperience);
        while (level < MaximumLevel)
        {
            var required = ExperienceForNextLevel(level);
            if (required <= 0 || remaining < required) break;
            remaining -= required;
            ++level;
        }
        return level;
    }
}

public sealed class ProfessionConfiguration
{
    public static ProfessionConfiguration Instance { get; private set; } = new();
    public ProfessionDefinition[] Professions { get; set; } = [];

    public bool IsStructurallyValid =>
        (Professions ?? []).Length <= 512 &&
        (Professions ?? []).All(profession => profession is { IsStructurallyValid: true }) &&
        (Professions ?? []).Select(profession => profession.Id).Distinct().Count() == (Professions ?? []).Length &&
        (Professions ?? []).SelectMany(profession => profession.Resources ?? []).Select(link => link.ResourceId).Distinct().Count() ==
        (Professions ?? []).SelectMany(profession => profession.Resources ?? []).Count();

    public ProfessionDefinition? Find(Guid id) =>
        (Professions ?? []).FirstOrDefault(profession => profession.Id == id);

    public (ProfessionDefinition Profession, ProfessionResourceLink Link)? FindResource(Guid resourceId)
    {
        foreach (var profession in Professions ?? [])
        {
            var link = (profession.Resources ?? []).FirstOrDefault(resource => resource.ResourceId == resourceId);
            if (link != null) return (profession, link);
        }
        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ProfessionConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new ProfessionConfiguration()
            : JsonSerializer.Deserialize<ProfessionConfiguration>(json, JsonOptions) ?? new ProfessionConfiguration();

        value.Professions ??= [];
        foreach (var profession in value.Professions)
        {
            profession.Name ??= "Profession";
            profession.Description ??= string.Empty;
            profession.Resources ??= [];
            profession.LevelRewards ??= [];
        }

        if (!value.IsStructurallyValid) throw new InvalidDataException("Invalid profession configuration.");
        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
