namespace Intersect.Framework.Core.MiniGames;

public sealed record PokerAnimationSet(
    Guid Deal = default, Guid Check = default, Guid Call = default, Guid Raise = default, Guid Fold = default,
    Guid AllIn = default, Guid Win = default, Guid Lose = default, Guid LevelUp = default,
    Guid Join = default, Guid Leave = default)
{
    public static PokerAnimationSet Empty { get; } = new();
}
