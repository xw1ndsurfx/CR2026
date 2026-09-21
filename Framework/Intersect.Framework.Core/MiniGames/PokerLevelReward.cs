namespace Intersect.Framework.Core.MiniGames;

public sealed record PokerLevelReward(int Level, Guid ItemId, int Quantity)
{
    public bool IsValid => Level is >= 2 and <= MiniGameProgression.MaximumLevel &&
        ItemId != Guid.Empty && Quantity is >= 1 and <= 1_000_000_000;
}

/// <summary>Value-equality wrapper so shared-table configuration comparisons are deterministic.</summary>
public sealed class PokerLevelRewardSet : IEquatable<PokerLevelRewardSet>
{
    public const int MaximumRewards = 64;
    public static PokerLevelRewardSet Empty { get; } = new([]);
    public PokerLevelReward[] Items { get; }
    public bool IsValid => Items.Length <= MaximumRewards && Items.All(r => r is { IsValid: true });

    public PokerLevelRewardSet(IEnumerable<PokerLevelReward>? rewards)
    {
        Items = (rewards ?? []).OrderBy(r => r.Level).ThenBy(r => r.ItemId).ThenBy(r => r.Quantity).ToArray();
    }

    public bool Equals(PokerLevelRewardSet? other) => other != null && Items.SequenceEqual(other.Items);
    public override bool Equals(object? obj) => obj is PokerLevelRewardSet other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var reward in Items) hash.Add(reward);
        return hash.ToHashCode();
    }
}
