namespace Intersect.Server.MiniGames.Lockpicking;

using Intersect.Framework.Core.Achievements;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;

internal readonly record struct LockpickingStats(
    long LocksPicked,
    long PerfectPicks,
    long PicksBroken,
    int HighestDifficulty,
    long FastestPickMilliseconds
);

internal static class LockpickingStatsRuntime
{
    private const byte LocksSalt = 0x23;
    private const byte PerfectSalt = 0x47;
    private const byte BrokenSalt = 0x69;
    private const byte DifficultySalt = 0x8B;
    private const byte FastestSalt = 0xAD;

    private static Guid StateId(Guid professionId, byte salt)
    {
        var bytes = professionId.ToByteArray();
        bytes[0] ^= salt;
        bytes[4] ^= (byte)(salt * 3);
        bytes[9] ^= (byte)(salt * 5);
        bytes[15] ^= (byte)(salt * 7);
        return new Guid(bytes);
    }

    private static long Get(Player player, Guid professionId, byte salt) =>
        Math.Max(0L, player.GetVariable(StateId(professionId, salt))?.Value?.Integer ?? 0L);

    private static void Set(Player player, Guid professionId, byte salt, long value) =>
        player.SetVariableValue(StateId(professionId, salt), Math.Max(0L, value));

    internal static LockpickingStats Get(Player player, Guid professionId) => new(
        Get(player, professionId, LocksSalt),
        Get(player, professionId, PerfectSalt),
        Get(player, professionId, BrokenSalt),
        (int)Math.Min(int.MaxValue, Get(player, professionId, DifficultySalt)),
        Get(player, professionId, FastestSalt)
    );

    internal static void RecordSuccess(
        Player player,
        Guid professionId,
        int difficulty,
        long elapsedMilliseconds,
        bool perfect,
        bool fast,
        bool pickBroken
    )
    {
        var stats = Get(player, professionId);
        Set(player, professionId, LocksSalt, SafeAdd(stats.LocksPicked, 1));
        Set(player, professionId, DifficultySalt, Math.Max(stats.HighestDifficulty, difficulty));

        if (perfect)
            Set(player, professionId, PerfectSalt, SafeAdd(stats.PerfectPicks, 1));

        if (elapsedMilliseconds > 0 &&
            (stats.FastestPickMilliseconds <= 0 || elapsedMilliseconds < stats.FastestPickMilliseconds))
        {
            Set(player, professionId, FastestSalt, elapsedMilliseconds);
        }

        AchievementRuntime.AddProgress(
            player,
            AchievementObjectiveType.LockpickingSuccesses,
            1
        );

        if (perfect)
        {
            AchievementRuntime.AddProgress(
                player,
                AchievementObjectiveType.LockpickingPerfects,
                1
            );
        }

        if (difficulty >= 5)
        {
            AchievementRuntime.AddProgress(
                player,
                AchievementObjectiveType.LockpickingDifficultyFive,
                1
            );
        }

        if (fast)
        {
            AchievementRuntime.AddProgress(
                player,
                AchievementObjectiveType.LockpickingFastPicks,
                1
            );
        }

        if (!pickBroken)
        {
            AchievementRuntime.AddProgress(
                player,
                AchievementObjectiveType.LockpickingNoBreakPicks,
                1
            );
        }
    }

    internal static void RecordBrokenPick(Player player, Guid professionId)
    {
        var stats = Get(player, professionId);
        Set(player, professionId, BrokenSalt, SafeAdd(stats.PicksBroken, 1));
        AchievementRuntime.AddProgress(
            player,
            AchievementObjectiveType.LockpickingBrokenPicks,
            1
        );
    }

    private static long SafeAdd(long value, long amount) =>
        value > long.MaxValue - amount ? long.MaxValue : value + amount;
}
