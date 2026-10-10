using Intersect.Server.Database;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.Leaderboards;

internal sealed record DungeonLeaderboardRow(
    string PlayerName,
    long Completions,
    long BestClearTimeMilliseconds
);

internal static partial class LeaderboardDataRuntime
{
    /// <summary>
    /// Rank the completed runs for a single configured dungeon. Only two
    /// variable IDs are fetched, and online values override stale DB snapshots.
    /// Public results deliberately contain character display names, not IDs.
    /// </summary>
    internal static IReadOnlyList<DungeonLeaderboardRow> Dungeons(
        Guid dungeonId,
        int limitPerDungeon,
        string? sort = null
    )
    {
        limitPerDungeon = Math.Clamp(limitPerDungeon, 1, 100);
        sort = (sort ?? "clears").Trim().ToLowerInvariant();

        var completionsVariableId = DungeonStatisticsRuntime.CompletionsVariableId(dungeonId);
        var bestTimeVariableId = DungeonStatisticsRuntime.BestClearTimeVariableId(dungeonId);

        var records = new Dictionary<Guid, (long Completions, long BestTime)>();
        var names = new Dictionary<Guid, string>();

        try
        {
            using var context = DbInterface.CreatePlayerContext();
            var variables = context.Player_Variables
                .AsNoTracking()
                .Include(variable => variable.Player)
                .Where(variable =>
                    variable.VariableId == completionsVariableId ||
                    variable.VariableId == bestTimeVariableId)
                .ToArray();

            foreach (var variable in variables)
            {
                if (variable.Player == null)
                    continue;

                var playerId = variable.PlayerId;
                names[playerId] = variable.Player.Name;
                records.TryGetValue(playerId, out var previous);
                var value = Math.Max(0L, variable.Value?.Integer ?? 0L);

                records[playerId] = variable.VariableId == completionsVariableId
                    ? (value, previous.BestTime)
                    : (previous.Completions, value);
            }
        }
        catch
        {
            // Prefer a partial online leaderboard to failing the public Wiki API
            // while the player database is temporarily unavailable.
        }

        // Active characters may not have been saved to disk yet. Their in-memory
        // values are the authoritative snapshot and must replace any persisted row.
        foreach (var player in Player.OnlinePlayersSnapshot())
        {
            names[player.Id] = player.Name;
            var current = DungeonStatisticsRuntime.ReadLeaderboardStats(player, dungeonId);
            records[player.Id] = (current.Completions, current.BestClearTimeMilliseconds);
        }

        var candidates = records
            .Select(entry => new
            {
                Name = names.GetValueOrDefault(entry.Key),
                entry.Value.Completions,
                BestClearTimeMilliseconds = entry.Value.BestTime,
            })
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Name) &&
                entry.Completions > 0 &&
                (sort != "fastest" || entry.BestClearTimeMilliseconds > 0));

        var ranked = sort == "fastest"
            ? candidates
                .OrderBy(entry => entry.BestClearTimeMilliseconds)
                .ThenByDescending(entry => entry.Completions)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            : candidates
                .OrderByDescending(entry => entry.Completions)
                .ThenBy(entry => entry.BestClearTimeMilliseconds <= 0
                    ? long.MaxValue : entry.BestClearTimeMilliseconds)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        return ranked
            .Take(limitPerDungeon)
            .Select(entry => new DungeonLeaderboardRow(
                entry.Name!,
                entry.Completions,
                entry.BestClearTimeMilliseconds
            ))
            .ToArray();
    }
}
