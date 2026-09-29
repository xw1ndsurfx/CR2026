using System.Text.Json;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.Leaderboards;

internal sealed record LeaderboardTitleStatus(
    int Rank,
    string Title,
    int ExperienceBonusPercent,
    int ConsecutiveDays
);

internal static class LeaderboardTitleRuntime
{
    internal const int QualificationDays = 3;
    internal const int FirstPlaceExperienceBonusPercent = 20;
    internal const int OtherTopThreeExperienceBonusPercent = 15;
    internal const string FirstPlaceTitle = "Champion du Royaume";
    internal const string OtherTopThreeTitle = "Élite du Royaume";

    private static readonly object Gate = new();
    private static readonly string StatePath = Path.Combine("resources", "leaderboard-title-state.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static PersistedState? _state;
    private static Dictionary<Guid, LeaderboardTitleStatus> _active = [];
    private static DateTimeOffset _nextRefreshAt = DateTimeOffset.MinValue;
    private static bool _hasSnapshot;

    internal static LeaderboardTitleStatus? GetStatus(Guid playerId)
    {
        EnsureFresh();
        lock (Gate)
            return _active.GetValueOrDefault(playerId);
    }

    internal static int GetExperienceBonusPercent(Guid playerId) =>
        GetStatus(playerId)?.ExperienceBonusPercent ?? 0;

    internal static void MarkRankingDirty()
    {
        lock (Gate)
        {
            var sooner = DateTimeOffset.UtcNow.AddSeconds(5);
            if (_nextRefreshAt > sooner)
                _nextRefreshAt = sooner;
        }
    }

    internal static IReadOnlyDictionary<Guid, LeaderboardTitleStatus> Snapshot()
    {
        EnsureFresh();
        lock (Gate)
            return new Dictionary<Guid, LeaderboardTitleStatus>(_active);
    }

    private static void EnsureFresh()
    {
        List<(Guid Id, LeaderboardTitleStatus? Before, LeaderboardTitleStatus? After)> changes = [];
        var shouldNotify = false;

        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _nextRefreshAt)
                return;

            _nextRefreshAt = now.AddSeconds(30);
            _state ??= LoadState();

            var topThree = ReadTopThree();
            var today = DateTime.Today;

            if (_state.LastEvaluationDate.Date != today)
            {
                var wasYesterday = _state.LastEvaluationDate.Date == today.AddDays(-1);
                var nextStreaks = new Dictionary<Guid, int>();

                foreach (var candidate in topThree)
                {
                    var streak = 1;
                    if (wasYesterday && _state.StreakDays.TryGetValue(candidate.Id, out var previous))
                        streak = previous + 1;

                    nextStreaks[candidate.Id] = streak;
                }

                _state.LastEvaluationDate = today;
                _state.StreakDays = nextStreaks;
                SaveState(_state);
            }

            var nextActive = new Dictionary<Guid, LeaderboardTitleStatus>();
            for (var index = 0; index < topThree.Length; ++index)
            {
                var candidate = topThree[index];
                var rank = index + 1;
                var streak = _state.StreakDays.GetValueOrDefault(candidate.Id);

                if (streak < QualificationDays)
                    continue;

                nextActive[candidate.Id] = new LeaderboardTitleStatus(
                    rank,
                    rank == 1 ? FirstPlaceTitle : OtherTopThreeTitle,
                    rank == 1 ? FirstPlaceExperienceBonusPercent : OtherTopThreeExperienceBonusPercent,
                    streak
                );
            }

            foreach (var id in _active.Keys.Union(nextActive.Keys).Distinct())
            {
                _active.TryGetValue(id, out var before);
                nextActive.TryGetValue(id, out var after);

                if (!EquivalentForPlayer(before, after))
                    changes.Add((id, before, after));
            }

            shouldNotify = _hasSnapshot && changes.Count > 0;
            _active = nextActive;
            _hasSnapshot = true;
        }

        if (!shouldNotify)
            return;

        foreach (var change in changes)
        {
            var player = Player.FindOnline(change.Id);
            if (player == null)
                continue;

            PacketSender.SendEntityDataToProximity(player);

            if (change.After == null && change.Before != null)
            {
                PacketSender.SendChatMsg(
                    player,
                    $"[Classement] Titre retiré : {change.Before.Title}.",
                    Enums.ChatMessageType.Notice,
                    Color.White
                );
            }
            else if (change.After != null)
            {
                var verb = change.Before == null ? "Titre obtenu" : "Titre mis à jour";
                PacketSender.SendChatMsg(
                    player,
                    $"[Classement] {verb} : {change.After.Title} (+{change.After.ExperienceBonusPercent}% EXP).",
                    Enums.ChatMessageType.Notice,
                    Color.White
                );
            }
        }
    }

    private static bool EquivalentForPlayer(
        LeaderboardTitleStatus? left,
        LeaderboardTitleStatus? right
    )
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null)
            return false;

        return string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
               left.ExperienceBonusPercent == right.ExperienceBonusPercent;
    }

    private static Candidate[] ReadTopThree()
    {
        using var context = DbInterface.CreatePlayerContext();

        var candidates = context.Players
            .AsNoTracking()
            .OrderByDescending(player => player.Level)
            .ThenByDescending(player => player.Exp)
            .ThenBy(player => player.Name)
            .Take(20)
            .Select(player => new Candidate(player.Id, player.Name, player.Level, player.Exp))
            .ToDictionary(player => player.Id);

        foreach (var online in Player.OnlinePlayersSnapshot())
        {
            candidates[online.Id] = new Candidate(online.Id, online.Name, online.Level, online.Exp);
        }

        return candidates.Values
            .OrderByDescending(player => player.Level)
            .ThenByDescending(player => player.Experience)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static PersistedState LoadState()
    {
        try
        {
            if (!File.Exists(StatePath))
                return new PersistedState();

            var state = JsonSerializer.Deserialize<PersistedState>(
                File.ReadAllText(StatePath),
                JsonOptions
            );

            return state ?? new PersistedState();
        }
        catch
        {
            return new PersistedState();
        }
    }

    private static void SaveState(PersistedState state)
    {
        try
        {
            var fullPath = Path.GetFullPath(StatePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var temp = fullPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temp, fullPath, overwrite: true);
        }
        catch
        {
            // A transient persistence failure must never interrupt gameplay.
        }
    }

    private sealed record Candidate(Guid Id, string Name, int Level, long Experience);

    private sealed class PersistedState
    {
        public DateTime LastEvaluationDate { get; set; } = DateTime.MinValue;
        public Dictionary<Guid, int> StreakDays { get; set; } = [];
    }
}
