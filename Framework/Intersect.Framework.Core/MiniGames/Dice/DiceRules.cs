namespace Intersect.Framework.Core.MiniGames.Dice;

public enum DiceBet { UnderSeven = 0, Seven = 1, OverSeven = 2 }

/// <summary>Multipliers are total returns including the original wager. XP never affects the dice.</summary>
public sealed record DiceRules(long MinimumBet = 25, long MaximumBet = 1000, long Step = 25,
    int OutsideReturn = 2, int SevenReturn = 5)
{
    public bool IsValid => MinimumBet >= 1 && MaximumBet >= MinimumBet && MaximumBet <= 20_000_000 &&
        Step >= 1 && Step <= MaximumBet && MinimumBet % Step == 0 && MaximumBet % Step == 0 &&
        OutsideReturn is >= 2 and <= 50 && SevenReturn is >= 2 and <= 50;
    public bool Accepts(long amount) => IsValid && amount >= MinimumBet && amount <= MaximumBet && amount % Step == 0;
    public int Multiplier(DiceBet bet) => bet switch
    {
        DiceBet.UnderSeven or DiceBet.OverSeven => OutsideReturn,
        DiceBet.Seven => SevenReturn,
        _ => throw new ArgumentOutOfRangeException(nameof(bet)),
    };
    public static bool IsFace(int face) => face is >= 1 and <= 6;
    public static DiceBet Category(int first, int second)
    {
        if (!IsFace(first) || !IsFace(second)) throw new ArgumentOutOfRangeException(nameof(first));
        return first + second < 7 ? DiceBet.UnderSeven : first + second == 7 ? DiceBet.Seven : DiceBet.OverSeven;
    }
    public long Return(DiceBet bet, long wager, int first, int second)
    {
        if (!Accepts(wager) || !Enum.IsDefined(bet)) throw new ArgumentOutOfRangeException(nameof(wager));
        return Category(first, second) == bet ? checked(wager * Multiplier(bet)) : 0;
    }
}
