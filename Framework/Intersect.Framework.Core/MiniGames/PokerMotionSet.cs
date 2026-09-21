namespace Intersect.Framework.Core.MiniGames;

public enum PokerMotionSpeed
{
    Off = 0,
    Fast = 1,
    Normal = 2,
    Cinematic = 3,
}

public sealed record PokerMotionSet(
    PokerMotionSpeed Speed = PokerMotionSpeed.Normal,
    bool DealCards = true,
    bool BoardCards = true,
    bool Chips = true,
    bool Showdown = true,
    bool Shuffle = true,
    bool AllIn = true)
{
    public static PokerMotionSet Default { get; } = new();
    public bool IsValid => Speed is >= PokerMotionSpeed.Off and <= PokerMotionSpeed.Cinematic;
    public bool Enabled => Speed != PokerMotionSpeed.Off;

    public int Duration(int normalMilliseconds) => Speed switch
    {
        PokerMotionSpeed.Off => 0,
        PokerMotionSpeed.Fast => Math.Max(80, normalMilliseconds / 2),
        PokerMotionSpeed.Cinematic => normalMilliseconds * 2,
        _ => normalMilliseconds,
    };
}
