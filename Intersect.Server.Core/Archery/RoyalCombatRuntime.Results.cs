using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intersect.Framework.Core.Archery;

namespace Intersect.Server.Archery;

internal sealed record ArcheryResult(Guid CompetitionId, Guid CharacterId, Guid RangeId, string SeasonKey,
    string RulesKey, string Name, int Score, int MaximumScore, DateTimeOffset CompletedUtc);

internal static partial class RoyalCombatRuntime
{
    private static long _archeryResultsRevision;
    internal static long ArcheryResultsRevision => Interlocked.Read(ref _archeryResultsRevision);
    // Increment physicsVersion when scoring/ballistics change. Do not mix incompatible cards.
    internal static string ArcheryRulesKey(ArcheryRules rules, double wind) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { physicsVersion = 1, wind, rules }))));

    internal static void SaveArcheryResult(ArcheryResult result)
    {
        if (result.CompetitionId == Guid.Empty || result.CharacterId == Guid.Empty || result.RangeId == Guid.Empty ||
            result.Score < 0 || result.Score > result.MaximumScore || result.MaximumScore is < 10 or > 600 ||
            string.IsNullOrWhiteSpace(result.Name) || result.Name.Length > 100 || result.SeasonKey.Length is < 1 or > 40 ||
            result.RulesKey.Length != 64) throw new InvalidDataException("royal-archery-result");
        lock (Gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ArcheryResults(CompetitionId,CharacterId,RangeId,SeasonKey,RulesKey,PlayerName,Score,MaximumScore,CompletedUtc)
                VALUES($match,$character,$range,$season,$rules,$name,$score,$maximum,$time)
                ON CONFLICT(CompetitionId,CharacterId) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$match", result.CompetitionId.ToString("N"));
            command.Parameters.AddWithValue("$character", result.CharacterId.ToString("N"));
            command.Parameters.AddWithValue("$range", result.RangeId.ToString("N"));
            command.Parameters.AddWithValue("$season", result.SeasonKey); command.Parameters.AddWithValue("$rules", result.RulesKey);
            command.Parameters.AddWithValue("$name", result.Name); command.Parameters.AddWithValue("$score", result.Score);
            command.Parameters.AddWithValue("$maximum", result.MaximumScore); command.Parameters.AddWithValue("$time", result.CompletedUtc.ToUnixTimeSeconds());
            if (command.ExecuteNonQuery() > 0) Interlocked.Increment(ref _archeryResultsRevision);
        }
    }

    internal static ArcheryLeaderboardView ArcheryLeaderboard(ArcheryRange range, ArcheryLeaderboardPeriod period, int limit, DateTimeOffset now)
    {
        if (!Enum.IsDefined(period)) throw new ArgumentOutOfRangeException(nameof(period));
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var week = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var from = period == ArcheryLeaderboardPeriod.Day ? day : week;
        var until = period == ArcheryLeaderboardPeriod.Day ? day.AddDays(1) : week.AddDays(7);
        var view = new ArcheryLeaderboardView { RangeId = range.Id, SeasonKey = range.SeasonKey, Period = period, MaximumScore = range.Rules.MaximumScore };
        lock (Gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = """
                WITH cards AS (
                    SELECT *, ROW_NUMBER() OVER(PARTITION BY CharacterId ORDER BY CompletedUtc DESC,CompetitionId DESC) AS latest
                    FROM ArcheryResults WHERE RangeId=$range AND SeasonKey=$season AND RulesKey=$rules
                        AND CompletedUtc >= $from AND CompletedUtc < $until
                ), scores AS (
                    SELECT CharacterId, MAX(CASE WHEN latest=1 THEN PlayerName END) AS Name,
                        MAX(Score) AS BestScore, COUNT(*) AS Cards FROM cards GROUP BY CharacterId
                )
                SELECT RANK() OVER(ORDER BY BestScore DESC), Name, BestScore, Cards
                FROM scores ORDER BY BestScore DESC, Name COLLATE NOCASE, CharacterId LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$range", range.Id.ToString("N")); command.Parameters.AddWithValue("$season", range.SeasonKey);
            command.Parameters.AddWithValue("$rules", ArcheryRulesKey(range.Rules, range.Wind));
            command.Parameters.AddWithValue("$from", period == ArcheryLeaderboardPeriod.Season ? long.MinValue : from.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$until", period == ArcheryLeaderboardPeriod.Season ? now.ToUnixTimeSeconds() + 1 : Math.Min(until.ToUnixTimeSeconds(), now.ToUnixTimeSeconds() + 1));
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
            using var reader = command.ExecuteReader(); var entries = new List<ArcheryLeaderboardEntry>();
            while (reader.Read()) entries.Add(new() { Rank = reader.GetInt32(0), Name = reader.GetString(1), BestScore = reader.GetInt32(2), CompletedCards = reader.GetInt32(3) });
            view.Entries = entries.ToArray(); return view;
        }
    }
}
