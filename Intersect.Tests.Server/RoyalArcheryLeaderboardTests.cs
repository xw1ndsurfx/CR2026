using Intersect.Framework.Core.Archery;
using Intersect.Server.Archery;
using Intersect.Server.Leaderboards;
using Intersect.Server.Web.Controllers.Api.V1;
using Microsoft.AspNetCore.Mvc;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Network.Packets.Client;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Intersect.Tests.Server;

public sealed partial class RoyalCombatTests
{
    private sealed class ResultStore : IDisposable
    {
        private readonly string _original = RoyalCombatRuntime.ProgressPath;
        internal readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".db");
        internal ResultStore() => RoyalCombatRuntime.ProgressPath = Path;
        public void Dispose()
        {
            RoyalCombatRuntime.ProgressPath = _original;
            File.Delete(Path); File.Delete(Path + ".block");
        }
    }
    private static ArcheryResult Result(ArcheryRange range, Guid player, string name, int score, DateTimeOffset utc, Guid? match = null) =>
        new(match ?? Guid.NewGuid(), player, range.Id, range.SeasonKey, RoyalCombatRuntime.ArcheryRulesKey(range.Rules, range.Wind), name, score, range.Rules.MaximumScore, utc);

    [Test]
    public void AddingResultTablesPreservesAnExistingMasteryDatabase()
    {
        using var store = new ResultStore(); var player = Guid.NewGuid();
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={store.Path};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE WeaponMasteries(CharacterId TEXT NOT NULL,Category TEXT NOT NULL,Experience INTEGER NOT NULL,PRIMARY KEY(CharacterId,Category)); INSERT INTO WeaponMasteries VALUES($id,'archery',500);";
            command.Parameters.AddWithValue("$id", player.ToString("N")); command.ExecuteNonQuery();
        }
        RoyalCombatRuntime.SaveArcheryResult(Result(new(), player, "Existing character", 100, DateTimeOffset.UtcNow));
        Assert.That(RoyalCombatRuntime.Load(player)["archery"], Is.EqualTo(500));
    }

    [Test]
    public void ActualFinishedPlayerCardIsCommittedAndSurvivesLeavingTheCompetition()
    {
        using var store = new ResultStore(); using var world = new World();
        world.Range.CompetitionEnabled = world.Range.RankedCompetition = true; world.Range.MaximumPlayers = 2;
        world.Range.Rules = new() { Rounds = [new() { Arrows = 1, Seconds = 15 }] }; world.Player.Name = "Finisher";
        var other = new Player { Name = "Withdrawn", MapId = world.Map.Id, MapInstanceId = world.Player.MapInstanceId,
            X = 5, Y = 4, InGame = true, Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)) };
        GC.SuppressFinalize(other);
        try
        {
            world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.JoinCompetition, RangeId = world.Range.Id });
            other.HandleArcheryRequest(new() { Action = ArcheryAction.JoinCompetition, RangeId = world.Range.Id });
            var started = Match(other)!.Started; Tick(world.Player, started + 250);
            var match = Match(world.Player)!; var release = started + 1450;
            var yaw = -Math.Sin((release - started) / 700d) * .0015;
            var pitch = Math.Asin(ArcheryPhysics.Gravity * match.Distance / (55 * 55)) / 2 - Math.Cos((release - started) / 900d) * .0015;
            Assert.That(match.Input(true, 1, 0, 0, started + 250), Is.True);
            Assert.That(match.Input(false, 2, yaw, pitch, release), Is.True);
            Tick(world.Player, started + 1800); Tick(world.Player, started + 2050);
            Assert.That(match.Phase, Is.EqualTo(ArcheryPhase.Finished)); Assert.That(match.Score, Is.EqualTo(10));
            world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = match.Id });
            var board = RoyalCombatRuntime.ArcheryLeaderboard(world.Range, ArcheryLeaderboardPeriod.Season, 16, DateTimeOffset.UtcNow);
            Assert.That(board.Entries.Length, Is.EqualTo(1)); Assert.That(board.Entries[0].Name, Is.EqualTo("Finisher"));
            Assert.That(board.Entries[0].BestScore, Is.EqualTo(10)); Assert.That(world.Player.CanUseLobbyTravel, Is.True);
        }
        finally
        {
            other.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = Match(other)?.Id ?? Competition(other) }); other.Client = null;
        }
    }

    [Test]
    public void RankedCardsPersistOnceAndReopenWithBestPerCharacterAndSharedPlaces()
    {
        using var store = new ResultStore(); var range = new ArcheryRange(); var now = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var result = Result(range, a, "Original name", 100, now.AddHours(-2));
        Parallel.For(0, 8, _ => RoyalCombatRuntime.SaveArcheryResult(result));
        RoyalCombatRuntime.SaveArcheryResult(result with { Score = 150 });
        RoyalCombatRuntime.SaveArcheryResult(Result(range, a, "Renamed", 90, now.AddHours(-1)));
        RoyalCombatRuntime.SaveArcheryResult(Result(range, b, "B", 100, now));
        RoyalCombatRuntime.SaveArcheryResult(Result(range, c, "C", 80, now));
        var board = RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Day, 100, now);
        Assert.That(board.Entries.Length, Is.EqualTo(3));
        Assert.That(board.Entries.Single(e => e.Name == "Renamed").BestScore, Is.EqualTo(100));
        Assert.That(board.Entries.Single(e => e.Name == "Renamed").CompletedCards, Is.EqualTo(2));
        Assert.That(board.Entries.Select(e => e.Rank), Is.EqualTo(new[] { 1, 1, 3 }));
        // Every call closes its SQLite connection: this read is a persisted reload, not an online overlay.
        Assert.That(RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Season, 1, now).Entries.Length, Is.EqualTo(1));
        Assert.Throws<InvalidDataException>(() => RoyalCombatRuntime.SaveArcheryResult(result with { Score = -1 }));
    }
    [Test]
    public void CalendarUtcDayAndMondayWeekExcludeOtherSeasonsRangesRulesAndFutureCards()
    {
        using var store = new ResultStore(); var range = new ArcheryRange(); var now = DateTimeOffset.Parse("2026-10-27T00:30:00+02:00"); // Monday 22:30 UTC
        var player = Guid.NewGuid();
        RoyalCombatRuntime.SaveArcheryResult(Result(range, player, "Archer", 40, DateTimeOffset.Parse("2026-10-25T23:59:59Z")));
        RoyalCombatRuntime.SaveArcheryResult(Result(range, player, "Archer", 80, DateTimeOffset.Parse("2026-10-26T00:00:00Z")));
        RoyalCombatRuntime.SaveArcheryResult(Result(range, player, "Archer", 150, DateTimeOffset.Parse("2026-10-26T23:59:00Z")));
        var unrelated = Result(range, Guid.NewGuid(), "Unrelated", 150, now.AddMinutes(-1));
        RoyalCombatRuntime.SaveArcheryResult(unrelated with { SeasonKey = "old-season" });
        RoyalCombatRuntime.SaveArcheryResult(unrelated with { CompetitionId = Guid.NewGuid(), RangeId = Guid.NewGuid() });
        RoyalCombatRuntime.SaveArcheryResult(unrelated with { CompetitionId = Guid.NewGuid(), RulesKey = new string('F', 64) });
        foreach (var period in Enum.GetValues<ArcheryLeaderboardPeriod>())
        {
            var board = RoyalCombatRuntime.ArcheryLeaderboard(range, period, 16, now);
            Assert.That(board.Entries.Length, Is.EqualTo(1)); Assert.That(board.Entries[0].BestScore, Is.EqualTo(80));
            Assert.That(board.Entries[0].CompletedCards, Is.EqualTo(period == ArcheryLeaderboardPeriod.Season ? 2 : 1));
        }
        var tuesday = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        Assert.That(RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Day, 16, tuesday).Entries, Is.Empty);
        Assert.That(RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Week, 16, tuesday).Entries[0].BestScore, Is.EqualTo(150));
        var monday = DateTimeOffset.Parse("2026-11-02T00:00:00Z");
        Assert.That(RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Week, 16, monday).Entries, Is.Empty);
    }
    [Test]
    public void RulesFingerprintChangesWithPhysicalScoringAndRemainsStableAcrossConfigRoundTrip()
    {
        var range = new ArcheryRange(); var key = RoyalCombatRuntime.ArcheryRulesKey(range.Rules, range.Wind);
        var config = RoyalCombatConfiguration.FromJson(new RoyalCombatConfiguration { Ranges = [range] }.ToJson());
        Assert.That(RoyalCombatRuntime.ArcheryRulesKey(config.Ranges[0].Rules, 0), Is.EqualTo(key));
        range.Rules.Rounds[0].Targets[0].MotionX = 1;
        Assert.That(RoyalCombatRuntime.ArcheryRulesKey(range.Rules, 0), Is.Not.EqualTo(key));
        range = new(); Assert.That(RoyalCombatRuntime.ArcheryRulesKey(range.Rules, 1), Is.Not.EqualTo(key));
        range.Rules.Rounds[0].Seconds = 15;
        Assert.That(RoyalCombatRuntime.ArcheryRulesKey(range.Rules, 0), Is.Not.EqualTo(key));
    }
    [Test]
    public void RankedCompetitionQueuesOnlyCompletedCardsRetriesStorageAndPreservesItsSnapshot()
    {
        using var store = new ResultStore();
        var range = new ArcheryRange { CompetitionEnabled = true, RankedCompetition = true, MaximumPlayers = 2 };
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var instance = Guid.NewGuid();
        var id = ArcheryCompetitions.Join(range, instance, a, "A", 1000);
        ArcheryCompetitions.Join(range, instance, b, "B", 1001);
        ArcheryCompetitions.Poll(id, a, 1002, 100, true, false);
        ArcheryCompetitions.Poll(id, b, 1002, 150, false, true);
        File.WriteAllText(store.Path + ".block", "test");
        RoyalCombatRuntime.ProgressPath = System.IO.Path.Combine(store.Path + ".block", "scores.db");
        try
        {
            ArcheryCompetitions.FlushPending();
            Assert.That(ArcheryCompetitions.Poll(id, a, 1003, 150, true, false)!.View.SavePending, Is.True);
            RoyalCombatRuntime.ProgressPath = store.Path;
            typeof(ArcheryCompetitions).GetField("_retryAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, 0L);
            ArcheryCompetitions.FlushPending();
            Assert.That(ArcheryCompetitions.Poll(id, a, 1004, 150, true, false)!.View.SavePending, Is.False);
            var board = RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Season, 16, DateTimeOffset.UtcNow);
            Assert.That(board.Entries.Length, Is.EqualTo(1)); Assert.That(board.Entries[0].BestScore, Is.EqualTo(100));
            range.SeasonKey = "next-season";
            Assert.That(RoyalCombatRuntime.ArcheryLeaderboard(range, ArcheryLeaderboardPeriod.Season, 16, DateTimeOffset.UtcNow).Entries, Is.Empty);
        }
        finally { RoyalCombatRuntime.ProgressPath = store.Path; ArcheryCompetitions.Leave(id, a, 1005); ArcheryCompetitions.Leave(id, b, 1005); }
    }
    [Test]
    public void NativeLeaderboardGatewayAndWikiExposeNamesWithoutCharacterIdsAndRefreshAfterCommit()
    {
        using var store = new ResultStore(); using var world = new World(); world.Range.RankedCompetition = true;
        var first = LeaderboardDataRuntime.Archery(world.Range.Id, ArcheryLeaderboardPeriod.Day)!;
        Assert.That(first.Entries, Is.Empty);
        var player = Guid.NewGuid(); RoyalCombatRuntime.SaveArcheryResult(Result(world.Range, player, "Public archer", 120, DateTimeOffset.UtcNow));
        var controller = new WikiController();
        var response = (OkObjectResult)controller.ArcheryLeaderboards(world.Range.Id, "day", 500);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.That(json, Does.Contain("Public archer")); Assert.That(json, Does.Not.Contain(player.ToString()));
        Assert.That(json, Does.Not.Contain("CharacterId")); Assert.That(json, Does.Contain("120"));
        Assert.That(controller.ArcheryLeaderboards(world.Range.Id, "bad"), Is.TypeOf<BadRequestResult>());
        Assert.That(controller.ArcheryLeaderboards(Guid.NewGuid()), Is.TypeOf<NotFoundResult>());
        world.Range.RankedCompetition = false;
        Assert.That(LeaderboardDataRuntime.Archery(world.Range.Id, ArcheryLeaderboardPeriod.Day), Is.Null);
    }
    [Test]
    public void PublicLeaderboardReturnsServiceUnavailableOnStorageFailure()
    {
        using var store = new ResultStore(); using var world = new World(); world.Range.RankedCompetition = true;
        File.WriteAllText(store.Path + ".block", "test");
        RoyalCombatRuntime.ProgressPath = System.IO.Path.Combine(store.Path + ".block", "scores.db");
        var response = (StatusCodeResult)new WikiController().ArcheryLeaderboards(world.Range.Id);
        Assert.That(response.StatusCode, Is.EqualTo(503));
    }
}
