namespace Intersect.Framework.Core.MiniGames;

public enum PokerEffectKind
{
    Join = 0,
    Deal = 1,
    Check = 2,
    Call = 3,
    Raise = 4,
    Fold = 5,
    AllIn = 6,
    Victory = 7,
    LevelUp = 8,
    Leave = 9,
    YourTurn = 10,
    Flop = 11,
    Turn = 12,
    River = 13,
}

public static class PokerEffects
{
    public const int Count = 14;
    public static bool IsValid(PokerEffectKind kind) => (int)kind >= 0 && (int)kind < Count;
}
