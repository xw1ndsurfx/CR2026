using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.Leaderboards;

internal static partial class LeaderboardDataRuntime
{
    /// <summary>
    /// Fetch one read-only snapshot for all registered dungeons. This is invoked
    /// only when a player explicitly opens or refreshes the Dungeon panel;
    /// routine dungeon-state broadcasts must not query the whole player DB.
    /// </summary>
    internal static DungeonPodiumEntry[] DungeonPodiums()
    {
        var dungeonIds = DungeonConfigurationRuntime.Current.Dungeons
            .Select(dungeon => dungeon.Id)
            .Distinct()
            .ToArray();

        if (dungeonIds.Length == 0)
            return [];

        var metricIds = new Dictionary<Guid, (Guid DungeonId, bool IsCompletions)>();
        foreach (var dungeonId in dungeonIds)
        {
            metricIds[DungeonStatisticsRuntime.CompletionsVariableId(dungeonId)] =
                (dungeonId, true);
            metricIds[DungeonStatisticsRuntime.BestClearTimeVariableId(dungeonId)] =
                (dungeonId, false);
        }

        var names = new Dictionary<Guid, string>();
        var scores = new Dictionary<(Guid DungeonId, Guid PlayerId), (long Clears, long BestTime)>();
        var allMetricIds = metricIds.Keys.ToArray();

        try
        {
            using var context = DbInterface.CreatePlayerContext();
            var persisted = context.Player_Variables
                .AsNoTracking()
                .Include(variable => variable.Player)
                .Where(variable => allMetricIds.Contains(variable.VariableId))
                .ToArray();

            foreach (var variable in persisted)
            {
                if (variable.Player == null ||
                    !metricIds.TryGetValue(variable.VariableId, out var metric))
                    continue;

                names[variable.PlayerId] = variable.Player.Name;
                var key = (metric.DungeonId, variable.PlayerId);
                scores.TryGetValue(key, out var previous);
                var value = Math.Max(0L, variable.Value?.Integer ?? 0L);
                scores[key] = metric.IsCompletions
                    ? (value, previous.BestTime)
                    : (previous.Clears, value);
            }
        }
        catch
        {
            // Database unavailable: publish the live online subset rather than
            // preventing the player from opening their Dungeons window.
        }

        // Online characters have more recent, unsaved stats than the DB;
        // overwrite their persisted values (including clearing stale zeroes).
        foreach (var player in Player.OnlinePlayersSnapshot())
        {
            names[player.Id] = player.Name;
            foreach (var dungeonId in dungeonIds)
            {
                var latest = DungeonStatisticsRuntime.ReadLeaderboardStats(player, dungeonId);
                var key = (dungeonId, player.Id);
                if (latest.Completions > 0)
                {
                    scores[key] = (latest.Completions, latest.BestClearTimeMilliseconds);
                }
                else
                {
                    scores.Remove(key);
                }
            }
        }

        var byDungeon = scores
            .Where(row =>
                row.Value.Clears > 0 &&
                names.TryGetValue(row.Key.PlayerId, out var name) &&
                !string.IsNullOrWhiteSpace(name))
            .Select(row => new
            {
                row.Key.DungeonId,
                Name = names[row.Key.PlayerId],
                row.Value.Clears,
                row.Value.BestTime,
            })
            .GroupBy(row => row.DungeonId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var podiums = new List<DungeonPodiumEntry>();
        foreach (var dungeonId in dungeonIds)
        {
            if (!byDungeon.TryGetValue(dungeonId, out var players))
                continue;

            var mostClears = players
                .OrderByDescending(player => player.Clears)
                .ThenBy(player => player.BestTime <= 0 ? long.MaxValue : player.BestTime)
                .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(player => new DungeonPodiumPlayerEntry(
                    player.Name, player.Clears, player.BestTime))
                .ToArray();

            var fastest = players
                .Where(player => player.BestTime > 0)
                .OrderBy(player => player.BestTime)
                .ThenByDescending(player => player.Clears)
                .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(player => new DungeonPodiumPlayerEntry(
                    player.Name, player.Clears, player.BestTime))
                .ToArray();

            podiums.Add(new DungeonPodiumEntry(dungeonId, mostClears, fastest));
        }

        return podiums.ToArray();
    }
}
