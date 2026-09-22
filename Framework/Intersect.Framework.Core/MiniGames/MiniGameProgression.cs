namespace Intersect.Framework.Core.MiniGames;

/// <summary>Shared display rules. Awards and unlock authorization belong to the server.</summary>
public static class MiniGameProgression
{
    public const string Poker = "poker";
    public const string Blackjack = "blackjack";
    public const int ExperiencePerWin = 25;
    public const int MaximumLevel = 25;
    public const long MaximumExperience = 30_000;
    public const int BackCount = 6;

    public static long ExperienceAtLevel(int level)
    {
        if (level < 1 || level > MaximumLevel) throw new ArgumentOutOfRangeException(nameof(level));
        return 50L * level * (level - 1);
    }
    public static int Level(long experience)
    {
        experience = Math.Clamp(experience, 0, MaximumExperience);
        var level = 1;
        while (level < MaximumLevel && experience >= ExperienceAtLevel(level + 1)) ++level;
        return level;
    }
    public static int BackLevel(int backId) => backId switch
    {
        0 => 1, 1 => 5, 2 => 10, 3 => 15, 4 => 20, 5 => 25,
        _ => throw new ArgumentOutOfRangeException(nameof(backId)),
    };
    public static bool IsBack(int backId) => backId >= 0 && backId < BackCount;
    public static bool IsUnlocked(int backId, long experience) => IsBack(backId) &&
        experience >= 0 && experience <= MaximumExperience && Level(experience) >= BackLevel(backId);
}
