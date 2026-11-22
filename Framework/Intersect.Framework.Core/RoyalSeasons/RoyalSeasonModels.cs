using Newtonsoft.Json;

namespace Intersect.Framework.Core.RoyalSeasons;

public enum SeasonObjectiveKind { DungeonsCompleted, ModifiedDungeonsCompleted, NavalExpeditionsCompleted, ProfessionExperience }
public enum SeasonAction { Open, Refresh, ClaimFree, ClaimPremium }

public sealed class SeasonObjective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public SeasonObjectiveKind Kind { get; set; }
    public Guid TargetId { get; set; }
    public int Required { get; set; } = 1;
    public int Points { get; set; } = 100;
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100 &&
        Enum.IsDefined(Kind) && Required is >= 1 and <= 1000000 && (Kind == SeasonObjectiveKind.ProfessionExperience || Required <= 1000) && Points is >= 1 and <= 100000;
}
public sealed class SeasonReward
{
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    [JsonIgnore] public bool IsValid => ItemId == Guid.Empty ? Quantity is >= 0 and <= 1000000 : Quantity is >= 1 and <= 1000000;
}
public sealed class SeasonTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Points { get; set; } = 100;
    public SeasonReward Free { get; set; } = new();
    public SeasonReward Premium { get; set; } = new();
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100 &&
        Points is >= 1 and <= 10000000 && Free is { IsValid: true } && Premium is { IsValid: true } &&
        (Free.ItemId != Guid.Empty || Premium.ItemId != Guid.Empty);
}
public sealed class RoyalSeason
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    public DateTimeOffset EndsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(31);
    public int ClaimGraceDays { get; set; } = 7;
    public SeasonObjective[] Objectives { get; set; } = [];
    public SeasonTier[] Tiers { get; set; } = [];
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100 &&
        Description is { Length: <= 2000 } && StartsAt.Year is >= 2020 and <= 2100 && EndsAt > StartsAt &&
        EndsAt - StartsAt <= TimeSpan.FromDays(366) && ClaimGraceDays is >= 0 and <= 30 &&
        Objectives is { Length: >= 1 and <= 100 } && Objectives.All(o => o is { IsValid: true }) &&
        Objectives.Select(o => o.Id).Distinct().Count() == Objectives.Length &&
        Tiers is { Length: >= 1 and <= 100 } && Tiers.All(t => t is { IsValid: true }) &&
        Tiers.Select(t => t.Id).Distinct().Count() == Tiers.Length &&
        Tiers.Any(t => t.Free.ItemId != Guid.Empty) &&
        Tiers.Select(t => t.Points).SequenceEqual(Tiers.Select(t => t.Points).Distinct().Order()) &&
        Tiers.Last().Points <= Objectives.Sum(o => o.Points);
    public bool IsActive(DateTimeOffset now) => Enabled && now >= StartsAt && now < EndsAt;
    public bool CanClaim(DateTimeOffset now) => Enabled && now >= StartsAt && now < EndsAt.AddDays(ClaimGraceDays);
}
public sealed class RoyalSeasonConfiguration
{
    public bool Enabled { get; set; }
    public RoyalSeason[] Seasons { get; set; } = [];
    [JsonIgnore] public bool IsValid => Seasons is { Length: <= 64 } && Seasons.All(s => s is { IsValid: true }) &&
        Seasons.Select(s => s.Id).Distinct().Count() == Seasons.Length &&
        !Seasons.Where(s => s.Enabled).Any(a => Seasons.Any(b => b.Enabled && a.Id != b.Id && a.StartsAt < b.EndsAt && b.StartsAt < a.EndsAt));
}
public sealed class SeasonProgress
{
    public Dictionary<Guid, int> Objectives { get; set; } = [];
    public HashSet<Guid> ClaimedFree { get; set; } = [];
    public HashSet<Guid> ClaimedPremium { get; set; } = [];
    // Completion evidence is persisted with progress, including expedition reward receipts.
    public HashSet<Guid> Completions { get; set; } = [];
}
public sealed class RoyalSeasonProfile
{
    public Dictionary<Guid, SeasonProgress> Seasons { get; set; } = [];
}
public static class RoyalSeasonRules
{
    public static string Json(object value) => JsonConvert.SerializeObject(value);
    public static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { MaxDepth = 16, TypeNameHandling = TypeNameHandling.None })
        ?? throw new InvalidDataException("Invalid season data.");
    public static int Points(RoyalSeason season, SeasonProgress progress) => season.Objectives
        .Where(o => progress.Objectives.GetValueOrDefault(o.Id) >= o.Required).Sum(o => o.Points);
    public static bool Advance(RoyalSeason season, SeasonProgress progress, SeasonObjectiveKind kind, Guid target, long amount, DateTimeOffset now, Guid evidence = default)
    {
        if (!season.IsActive(now) || amount <= 0 || !Enum.IsDefined(kind) || evidence != Guid.Empty && progress.Completions.Contains(evidence)) return false;
        var changed = false;
        foreach (var objective in season.Objectives.Where(o => (o.Kind == kind || kind == SeasonObjectiveKind.ModifiedDungeonsCompleted && o.Kind == SeasonObjectiveKind.DungeonsCompleted) && (o.TargetId == Guid.Empty || o.TargetId == target)))
        {
            var before = Math.Clamp(progress.Objectives.GetValueOrDefault(objective.Id), 0, objective.Required);
            var next = (int)(before + Math.Min(objective.Required - before, amount));
            if (next == before) continue;
            progress.Objectives[objective.Id] = next; changed = true;
        }
        if (changed && evidence != Guid.Empty) progress.Completions.Add(evidence);
        return changed;
    }
    public static bool CanClaim(RoyalSeason season, SeasonProgress progress, SeasonTier tier, bool premiumTrack, bool premiumActive, DateTimeOffset now) =>
        season.CanClaim(now) && season.Tiers.Any(t => t.Id == tier.Id) && Points(season, progress) >= tier.Points &&
        (!premiumTrack || premiumActive) && !(premiumTrack ? progress.ClaimedPremium : progress.ClaimedFree).Contains(tier.Id) &&
        (premiumTrack ? tier.Premium : tier.Free).ItemId != Guid.Empty;
}
public sealed class RoyalSeasonView
{
    public bool Enabled { get; set; }
    public bool Premium { get; set; }
    public DateTimeOffset Now { get; set; }
    public RoyalSeason[] Seasons { get; set; } = [];
    public RoyalSeasonProfile Profile { get; set; } = new();
    public Dictionary<Guid, string> ItemNames { get; set; } = [];
    public string Message { get; set; } = "";
}
