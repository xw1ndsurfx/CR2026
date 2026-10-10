using System.Security.Cryptography;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;

namespace Intersect.Server.Dungeons;

/// <summary>
/// Per-character dungeon history stored in existing persistent player variables.
/// Keys are namespaced and derived from the dungeon ID, so editing or sorting
/// dungeon definitions never moves a player's statistics to another dungeon.
/// </summary>
internal static class DungeonStatisticsRuntime
{
    private static readonly Guid NamespaceId = new("e0ae80b7-3261-429c-a983-408df6a2a714");

    private enum Metric : byte
    {
        Attempts = 1,
        Completions = 2,
        Failures = 3,
        Deaths = 4,
        BestClearTimeMilliseconds = 5,
        LastCompletedUnixMilliseconds = 6,
    }

    private static Guid VariableId(Guid dungeonId, Metric metric)
    {
        Span<byte> input = stackalloc byte[33];
        NamespaceId.TryWriteBytes(input[..16]);
        dungeonId.TryWriteBytes(input.Slice(16, 16));
        input[32] = (byte)metric;
        return new Guid(SHA256.HashData(input).AsSpan(0, 16));
    }

    private static long Read(Player player, Guid dungeonId, Metric metric) =>
        Math.Max(0, player.GetVariable(VariableId(dungeonId, metric))?.Value?.Integer ?? 0L);

    private static void Write(Player player, Guid dungeonId, Metric metric, long value) =>
        player.SetVariableValue(VariableId(dungeonId, metric), Math.Max(0L, value));

    private static void Increment(Player player, Guid dungeonId, Metric metric)
    {
        var current = Read(player, dungeonId, metric);
        if (current < long.MaxValue)
            Write(player, dungeonId, metric, current + 1);
    }

    internal static void RecordAttempt(Player player, Guid dungeonId) =>
        Increment(player, dungeonId, Metric.Attempts);

    internal static void RecordDeath(Player player, Guid dungeonId) =>
        Increment(player, dungeonId, Metric.Deaths);

    internal static void RecordFailure(Player player, Guid dungeonId) =>
        Increment(player, dungeonId, Metric.Failures);

    internal static void RecordCompletion(Player player, Guid dungeonId, long clearTimeMilliseconds)
    {
        Increment(player, dungeonId, Metric.Completions);

        if (clearTimeMilliseconds > 0)
        {
            var previousBest = Read(player, dungeonId, Metric.BestClearTimeMilliseconds);
            if (previousBest == 0 || clearTimeMilliseconds < previousBest)
                Write(player, dungeonId, Metric.BestClearTimeMilliseconds, clearTimeMilliseconds);
        }

        Write(
            player,
            dungeonId,
            Metric.LastCompletedUnixMilliseconds,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );
    }

    internal static DungeonPlayerStatEntry[] BuildState(Player player)
    {
        var entries = new List<DungeonPlayerStatEntry>();
        foreach (var dungeon in DungeonConfigurationRuntime.Current.Dungeons)
        {
            var attempts = Read(player, dungeon.Id, Metric.Attempts);
            var completions = Read(player, dungeon.Id, Metric.Completions);
            var failures = Read(player, dungeon.Id, Metric.Failures);
            var deaths = Read(player, dungeon.Id, Metric.Deaths);
            var bestTime = Read(player, dungeon.Id, Metric.BestClearTimeMilliseconds);
            var lastClear = Read(player, dungeon.Id, Metric.LastCompletedUnixMilliseconds);

            // No need to send empty records for dungeons a character has never entered.
            if (attempts == 0 && completions == 0 && failures == 0 &&
                deaths == 0 && bestTime == 0 && lastClear == 0)
                continue;

            entries.Add(new DungeonPlayerStatEntry(
                dungeon.Id, attempts, completions, failures, deaths, bestTime, lastClear
            ));
        }

        return entries.ToArray();
    }
}
