using Intersect.Framework.Core.Archery;
using Intersect.Server.Archery;

namespace Intersect.Server.Leaderboards;

internal static partial class LeaderboardDataRuntime
{
    private static readonly object ArcheryBoardGate = new();
    private static readonly Dictionary<string, (long Until, ArcheryLeaderboardView View)> ArcheryBoards = new();
    internal static ArcheryLeaderboardView? Archery(Guid rangeId, ArcheryLeaderboardPeriod period, int limit = 16)
    {
        if (!Enum.IsDefined(period)) throw new ArgumentOutOfRangeException(nameof(period));
        var range = RoyalCombatRuntime.Current.Ranges.FirstOrDefault(r => r.Id == rangeId && r.Enabled && r.RankedCompetition);
        if (range == null) return null;
        ArcheryCompetitions.FlushPending();
        var now = DateTimeOffset.UtcNow;
        var key = $"{RoyalCombatRuntime.ProgressPath}|{range.Id}|{range.SeasonKey}|{RoyalCombatRuntime.ArcheryRulesKey(range.Rules, range.Wind)}|{period}|{now.UtcDateTime:yyyyMMdd}|{RoyalCombatRuntime.ArcheryResultsRevision}";
        lock (ArcheryBoardGate)
        {
            if (!ArcheryBoards.TryGetValue(key, out var cached) || Environment.TickCount64 >= cached.Until)
            {
                if (ArcheryBoards.Count >= 256) ArcheryBoards.Clear();
                cached = (Environment.TickCount64 + 5000, RoyalCombatRuntime.ArcheryLeaderboard(range, period, 100, now));
                ArcheryBoards[key] = cached;
            }
            return new() { RangeId = cached.View.RangeId, SeasonKey = cached.View.SeasonKey, Period = period,
                MaximumScore = cached.View.MaximumScore, Entries = cached.View.Entries.Take(Math.Clamp(limit, 1, 100)).ToArray() };
        }
    }
}
