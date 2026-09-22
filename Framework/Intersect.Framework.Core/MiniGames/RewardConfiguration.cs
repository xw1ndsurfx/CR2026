using Newtonsoft.Json;

namespace Intersect.Framework.Core.MiniGames;

public sealed record DailyRewardEntry(int Day, Guid ItemId, int Quantity)
{
    public bool IsValid(int cycleDays) =>
        Day >= 1 && Day <= cycleDays &&
        ItemId != Guid.Empty &&
        Quantity is >= 1 and <= 1_000_000_000;
}

/// <summary>
/// Global reward configuration. Mini-game level rewards intentionally live here rather
/// than on individual Start Mini-Game event commands so every table shares one progression.
/// </summary>
public sealed class RewardConfiguration
{
    public const int MaximumDailyCycleDays = 31;

    public static RewardConfiguration Instance { get; private set; } = new();

    public int DailyCycleDays { get; set; } = 7;
    public DailyRewardEntry[] DailyRewards { get; set; } = [];
    public PokerLevelReward[] PokerLevelRewards { get; set; } = [];
    public PokerLevelReward[] BlackjackLevelRewards { get; set; } = [];

    public bool IsStructurallyValid =>
        DailyCycleDays is >= 1 and <= MaximumDailyCycleDays &&
        (DailyRewards ?? []).Length <= MaximumDailyCycleDays * 8 &&
        (DailyRewards ?? []).All(reward => reward is { } && reward.IsValid(DailyCycleDays)) &&
        new PokerLevelRewardSet(PokerLevelRewards ?? []).IsValid &&
        new PokerLevelRewardSet(BlackjackLevelRewards ?? []).IsValid;

    public PokerLevelRewardSet CreatePokerLevelRewardSet() => new(PokerLevelRewards ?? []);
    public PokerLevelRewardSet CreateBlackjackLevelRewardSet() => new(BlackjackLevelRewards ?? []);

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

    public static RewardConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new RewardConfiguration()
            : JsonConvert.DeserializeObject<RewardConfiguration>(json) ?? new RewardConfiguration();

        value.DailyRewards ??= [];
        value.PokerLevelRewards ??= [];
        value.BlackjackLevelRewards ??= [];
        if (!value.IsStructurallyValid)
        {
            throw new InvalidDataException("Invalid reward configuration.");
        }

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
