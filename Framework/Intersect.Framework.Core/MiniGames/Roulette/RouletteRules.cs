namespace Intersect.Framework.Core.MiniGames.Roulette;

public enum RouletteBetType
{
    Straight = 0,
    Red = 1,
    Black = 2,
    Even = 3,
    Odd = 4,
    Low = 5,
    High = 6,
    Dozen1 = 7,
    Dozen2 = 8,
    Dozen3 = 9,
}

public static class RouletteRules
{
    public const int MinimumNumber = 0;
    public const int MaximumNumber = 36;
    public const long MaximumBalance = 1_000_000_000_000L;

    private static readonly HashSet<int> RedNumbers =
    [
        1, 3, 5, 7, 9, 12, 14, 16, 18,
        19, 21, 23, 25, 27, 30, 32, 34, 36,
    ];

    public static bool IsValidNumber(int number) => number is >= MinimumNumber and <= MaximumNumber;

    public static bool IsRed(int number) => RedNumbers.Contains(number);

    public static bool IsBlack(int number) => number != 0 && !IsRed(number);

    public static bool IsValidBet(RouletteBetType type, int number) =>
        type is >= RouletteBetType.Straight and <= RouletteBetType.Dozen3 &&
        (type != RouletteBetType.Straight || IsValidNumber(number));

    public static bool Wins(RouletteBetType type, int selectedNumber, int result) =>
        type switch
        {
            RouletteBetType.Straight => selectedNumber == result,
            RouletteBetType.Red => IsRed(result),
            RouletteBetType.Black => IsBlack(result),
            RouletteBetType.Even => result != 0 && result % 2 == 0,
            RouletteBetType.Odd => result % 2 == 1,
            RouletteBetType.Low => result is >= 1 and <= 18,
            RouletteBetType.High => result is >= 19 and <= 36,
            RouletteBetType.Dozen1 => result is >= 1 and <= 12,
            RouletteBetType.Dozen2 => result is >= 13 and <= 24,
            RouletteBetType.Dozen3 => result is >= 25 and <= 36,
            _ => false,
        };

    /// <summary>Net profit multiplier. The original wager is not included.</summary>
    public static int NetMultiplier(RouletteBetType type) =>
        type switch
        {
            RouletteBetType.Straight => 35,
            RouletteBetType.Dozen1 or RouletteBetType.Dozen2 or RouletteBetType.Dozen3 => 2,
            RouletteBetType.Red or RouletteBetType.Black or RouletteBetType.Even or RouletteBetType.Odd
                or RouletteBetType.Low or RouletteBetType.High => 1,
            _ => 0,
        };

    public static string BetName(RouletteBetType type, int number) =>
        type switch
        {
            RouletteBetType.Straight => $"Number {number}",
            RouletteBetType.Red => "Red",
            RouletteBetType.Black => "Black",
            RouletteBetType.Even => "Even",
            RouletteBetType.Odd => "Odd",
            RouletteBetType.Low => "1-18",
            RouletteBetType.High => "19-36",
            RouletteBetType.Dozen1 => "1st 12",
            RouletteBetType.Dozen2 => "2nd 12",
            RouletteBetType.Dozen3 => "3rd 12",
            _ => "Unknown",
        };
}
