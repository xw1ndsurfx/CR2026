using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Intersect.Framework.Core.RoyalContracts;

public enum RoyalContractObjective { NpcKills, DeliverItems, ExploreMaps }
public enum RoyalContractPeriod { Daily, Weekly }
public enum RoyalContractAction { Refresh, Accept, Deliver, Claim }

public sealed class RoyalContractTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Royal commission";
    public string Description { get; set; } = "Serve the Crown and earn its favour.";
    public RoyalContractPeriod Period { get; set; }
    public RoyalContractObjective Objective { get; set; }
    public bool Collective { get; set; }
    public Guid TargetId { get; set; }
    public int MinimumLevel { get; set; } = 1;
    public int MinimumQuantity { get; set; } = 10;
    public int MaximumQuantity { get; set; } = 20;
    public int Weight { get; set; } = 1;
    public int Seals { get; set; } = 25;
    public Guid RewardItemId { get; set; }
    public int RewardQuantity { get; set; }
    public override string ToString() => Name;
}

public sealed class RoyalContractsConfiguration
{
    public bool Enabled { get; set; } = true;
    public int DailySlots { get; set; } = 3;
    public int WeeklySlots { get; set; } = 2;
    public int ResetHourUtc { get; set; } = 5;
    public List<RoyalContractTemplate> Templates { get; set; } = [];

    public void Validate()
    {
        if (DailySlots is < 0 or > 8 || WeeklySlots is < 0 or > 8 || ResetHourUtc is < 0 or > 23 ||
            Templates == null || Templates.Count > 256 || Templates.Any(t => t == null) ||
            Templates.Select(t => t.Id).Distinct().Count() != Templates.Count)
            throw new InvalidDataException("Invalid contract configuration.");
        foreach (var t in Templates)
            if (t.Id == Guid.Empty || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 100 || t.Description == null ||
                t.Description.Length > 600 || !Enum.IsDefined(t.Objective) || !Enum.IsDefined(t.Period) ||
                t.MinimumLevel is < 1 or > 100000 || t.MinimumQuantity < 1 || t.MaximumQuantity < t.MinimumQuantity ||
                t.MaximumQuantity > 1000000 || t.Weight is < 1 or > 1000 || t.Seals is < 0 or > 1000000 ||
                t.RewardQuantity is < 0 or > 1000000 || (t.RewardItemId == Guid.Empty) != (t.RewardQuantity == 0) ||
                (t.Objective == RoyalContractObjective.DeliverItems && t.TargetId == Guid.Empty) ||
                (t.Objective == RoyalContractObjective.ExploreMaps && t.TargetId != Guid.Empty && t.MaximumQuantity != 1))
                throw new InvalidDataException("Invalid contract template.");
    }

    public static RoyalContractsConfiguration CreateDefault() => new()
    {
        Templates =
        [
            new() { Id = Guid.Parse("9067b78d-01da-4f5b-9755-87051830a101"), Name = "The Crown's hunt", Objective = RoyalContractObjective.NpcKills },
            new() { Id = Guid.Parse("9067b78d-01da-4f5b-9755-87051830a102"), Name = "Beyond the horizon", Objective = RoyalContractObjective.ExploreMaps, MinimumQuantity = 3, MaximumQuantity = 5, Seals = 35 },
            new() { Id = Guid.Parse("9067b78d-01da-4f5b-9755-87051830a103"), Name = "The royal guard", Objective = RoyalContractObjective.NpcKills, MinimumQuantity = 25, MaximumQuantity = 40, Seals = 50 },
            new() { Id = Guid.Parse("9067b78d-01da-4f5b-9755-87051830a104"), Name = "United for the kingdom", Period = RoyalContractPeriod.Weekly, Collective = true, Objective = RoyalContractObjective.NpcKills, MinimumQuantity = 500, MaximumQuantity = 750, Seals = 200 },
            new() { Id = Guid.Parse("9067b78d-01da-4f5b-9755-87051830a105"), Name = "Royal cartographers", Period = RoyalContractPeriod.Weekly, Objective = RoyalContractObjective.ExploreMaps, MinimumQuantity = 10, MaximumQuantity = 15, Seals = 150 },
        ],
    };
}

public sealed class RoyalContract
{
    public Guid Id { get; set; }
    public RoyalContractTemplate Template { get; set; } = new();
    public int Quantity { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class RoyalContractProgress
{
    public bool Accepted { get; set; }
    public bool Claimed { get; set; }
    public int Quantity { get; set; }
    public HashSet<Guid> VisitedMaps { get; set; } = [];
}

public sealed class RoyalContractProfile
{
    public long Seals { get; set; }
    public Dictionary<Guid, RoyalContractProgress> Contracts { get; set; } = [];
}

public sealed class RoyalContractView
{
    public RoyalContract Contract { get; set; } = new();
    public int Progress { get; set; }
    public int Contribution { get; set; }
    public bool Accepted { get; set; }
    public bool Claimed { get; set; }
    public bool Eligible { get; set; }
    public string TargetName { get; set; } = "";
    public string RewardName { get; set; } = "";
}

public sealed class RoyalContractBoardState
{
    public bool Enabled { get; set; }
    public long Seals { get; set; }
    public DateTimeOffset ServerTime { get; set; }
    public string Message { get; set; } = "";
    public List<RoyalContractView> Contracts { get; set; } = [];
}

/// <summary>Server-owned period snapshots: editing templates only affects the next rotation.</summary>
public sealed class RoyalContractRotation
{
    public DateTimeOffset DailyStart { get; set; }
    public DateTimeOffset WeeklyStart { get; set; }
    public List<RoyalContract> Daily { get; set; } = [];
    public List<RoyalContract> Weekly { get; set; } = [];
    public bool Refresh(RoyalContractsConfiguration configuration, DateTimeOffset now)
    {
        var changed = false;
        foreach (var period in Enum.GetValues<RoyalContractPeriod>())
        {
            var start = RoyalContractRules.PeriodStart(now, period, configuration.ResetHourUtc);
            if (start == (period == RoyalContractPeriod.Daily ? DailyStart : WeeklyStart)) continue;
            var generated = RoyalContractRules.Generate(configuration, period, start);
            if (period == RoyalContractPeriod.Daily) { DailyStart = start; Daily = generated; }
            else { WeeklyStart = start; Weekly = generated; }
            changed = true;
        }
        return changed;
    }
}

public static class RoyalContractRules
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 24 };
    public static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T Read<T>(string json) where T : new() => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException("Empty contract data.");
    public static Guid Identity(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("CR2026/RoyalContracts/" + key)).AsSpan(0, 16));
    public static Guid ContributionId(Guid contractId) => Identity("contribution/" + contractId);
    public static DateTimeOffset PeriodStart(DateTimeOffset now, RoyalContractPeriod period, int hour)
    {
        var shifted = now.ToUniversalTime().AddHours(-hour);
        var date = new DateTimeOffset(shifted.Year, shifted.Month, shifted.Day, hour, 0, 0, TimeSpan.Zero);
        return period == RoyalContractPeriod.Daily ? date : date.AddDays(-((int)shifted.DayOfWeek + 6) % 7);
    }
    private static ulong Hash(string key) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    public static List<RoyalContract> Generate(RoyalContractsConfiguration config, RoyalContractPeriod period, DateTimeOffset start)
    {
        config.Validate();
        var seed = $"{period}/{start.ToUnixTimeSeconds()}";
        // Weighted sampling without replacement; stable across platforms and restarts.
        var templates = config.Templates.Where(t => t.Enabled && t.Period == period)
            .OrderBy(t => -Math.Log((Hash(seed + t.Id) + 1d) / (ulong.MaxValue + 2d)) / t.Weight)
            .ThenBy(t => t.Id).Take(period == RoyalContractPeriod.Daily ? config.DailySlots : config.WeeklySlots);
        return templates.Select(t => new RoyalContract
        {
            Id = Identity(seed + "/" + t.Id), Template = Read<RoyalContractTemplate>(Json(t)),
            Quantity = t.MinimumQuantity + (int)(Hash(seed + "/quantity/" + t.Id) % (ulong)(t.MaximumQuantity - t.MinimumQuantity + 1)),
            ExpiresAt = start.AddDays(period == RoyalContractPeriod.Daily ? 1 : 7),
        }).ToList();
    }
    public static bool Advance(RoyalContract contract, RoyalContractProgress progress, RoyalContractObjective objective, Guid target, int amount, DateTimeOffset now)
    {
        if (!progress.Accepted || progress.Claimed || contract.ExpiresAt <= now || objective != contract.Template.Objective ||
            amount <= 0 || progress.Quantity >= contract.Quantity || (contract.Template.TargetId != Guid.Empty && contract.Template.TargetId != target)) return false;
        if (objective == RoyalContractObjective.ExploreMaps && (target == Guid.Empty || !progress.VisitedMaps.Add(target))) return false;
        var next = (int)Math.Min(contract.Quantity, (long)progress.Quantity + (objective == RoyalContractObjective.ExploreMaps ? 1 : amount));
        if (next == progress.Quantity) return false;
        progress.Quantity = next; return true;
    }
    public static bool CanClaim(RoyalContract contract, RoyalContractProgress progress, long total, DateTimeOffset now) =>
        progress.Accepted && !progress.Claimed && contract.ExpiresAt > now && progress.Quantity > 0 && total >= contract.Quantity;
}
