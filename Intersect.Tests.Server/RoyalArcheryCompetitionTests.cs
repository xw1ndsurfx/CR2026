using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Client;
using Intersect.Server.Archery;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using NUnit.Framework;

namespace Intersect.Tests.Server;

public sealed partial class RoyalCombatTests
{
    private static Guid Competition(Player player) => (Guid)typeof(Player).GetField("_archeryCompetition", Hidden)!.GetValue(player)!;
    [Test]
    public void RegistrationStartsAtDeadlineAndLostActiveEntrantsForfeit()
    {
        var range = new ArcheryRange { CompetitionEnabled = true, RegistrationSeconds = 10 };
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var instance = Guid.NewGuid();
        var id = ArcheryCompetitions.Join(range, instance, a, "A", 1000);
        ArcheryCompetitions.Join(range, instance, b, "B", 1001);
        ArcheryCompetitions.Poll(id, a, 9000, 0, false, false);
        Assert.That(ArcheryCompetitions.Poll(id, b, 9001, 0, false, false)!.View.Started, Is.False);
        var update = ArcheryCompetitions.Poll(id, a, 11000, 0, false, false)!;
        Assert.That(update.View.Started, Is.True); Assert.That(update.Started, Is.EqualTo(11000));
        update = ArcheryCompetitions.Poll(id, a, 19002, 5, true, false)!;
        Assert.That(update.View.Players.Single(p => p.Id == b).Forfeited, Is.True);
        Assert.That(update.View.Finished, Is.True);
        ArcheryCompetitions.Leave(id, a, 19003); ArcheryCompetitions.Leave(id, b, 19003);
    }
    [Test]
    public void ConcurrentRegistrationNeverOverbooksOrDuplicatesACharacter()
    {
        var range = new ArcheryRange { CompetitionEnabled = true };
        var instance = Guid.NewGuid(); var players = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
        var ids = new Guid[16];
        Parallel.For(0, 16, i => ids[i] = ArcheryCompetitions.Join(range, instance, players[i], "Archer", 1000));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(1));
        var board = ArcheryCompetitions.Poll(ids[0], players[0], 1001, 0, false, false)!.View;
        Assert.That(board.Started, Is.True); Assert.That(board.Players.Select(p => p.Id).Distinct().Count(), Is.EqualTo(16));
        Assert.That(ArcheryCompetitions.Join(range, instance, players[0], "Again", 1002), Is.EqualTo(Guid.Empty));
        foreach (var player in players) ArcheryCompetitions.Leave(ids[0], player, 1003);
    }
    [Test]
    public void CompetitionStartsAtCapacitySnapshotsRulesAndRanksTiesWithoutClientScores()
    {
        var range = new ArcheryRange { CompetitionEnabled = true, MaximumPlayers = 2 };
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var instance = Guid.NewGuid();
        var id = ArcheryCompetitions.Join(range, instance, a, "A", 1000);
        Assert.That(ArcheryCompetitions.Poll(id, a, 1001, 150, true, false)!.View.Started, Is.False);
        Assert.That(ArcheryCompetitions.Join(range, instance, b, "B", 1002), Is.EqualTo(id));
        range.Rules.Rounds[0].Distance = 100;
        var update = ArcheryCompetitions.Poll(id, a, 1003, 20, false, false)!;
        Assert.That(update.Started, Is.EqualTo(1002)); Assert.That(update.Rules.Rounds[0].Distance, Is.EqualTo(10));
        Assert.That(update.View.Players.Single(p => p.Id == a).Score, Is.EqualTo(20), "Prematch values cannot grant points.");
        ArcheryCompetitions.Poll(id, b, 1004, 20, true, false);
        update = ArcheryCompetitions.Poll(id, a, 1005, 20, true, false)!;
        Assert.That(update.View.Finished, Is.True); Assert.That(update.View.Players.All(p => p.Rank == 1), Is.True);
        update = ArcheryCompetitions.Poll(id, a, 1006, 150, true, false)!;
        Assert.That(update.View.Players.Single(p => p.Id == a).Score, Is.EqualTo(20), "Completed results are immutable.");
        ArcheryCompetitions.Leave(id, a, 1007); ArcheryCompetitions.Leave(id, b, 1007);
    }
    [Test]
    public void CompetitionRegistrationCancellationWithdrawalAndInstanceIsolation()
    {
        var range = new ArcheryRange { CompetitionEnabled = true, RegistrationSeconds = 10 };
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var instance = Guid.NewGuid();
        var id = ArcheryCompetitions.Join(range, instance, a, "A", 1000);
        Assert.That(ArcheryCompetitions.Join(range, instance, a, "A", 1001), Is.EqualTo(Guid.Empty));
        Assert.That(ArcheryCompetitions.Join(range, instance, b, "B", 1002), Is.EqualTo(id));
        var separate = ArcheryCompetitions.Join(range, Guid.NewGuid(), c, "C", 1003);
        Assert.That(separate, Is.Not.EqualTo(id));
        ArcheryCompetitions.Leave(id, b, 2000);
        Assert.That(ArcheryCompetitions.Poll(id, a, 10000, 0, false, false)!.View.Players.Length, Is.EqualTo(1));
        Assert.That(ArcheryCompetitions.Poll(id, a, 11001, 0, false, false)!.View.Cancelled, Is.True);
        Assert.That(ArcheryCompetitions.Poll(separate, c, 12000, 0, false, false), Is.Null, "Lost queued players expire.");
        ArcheryCompetitions.Leave(id, a, 12001);
    }
    [Test]
    public void CompetitionSupportsSixteenEntrantsAndForfeitsDoNotWin()
    {
        var range = new ArcheryRange { CompetitionEnabled = true };
        var instance = Guid.NewGuid(); var players = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
        var ids = players.Select((p, i) => ArcheryCompetitions.Join(range, instance, p, "Archer " + i, 1000 + i)).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(1));
        var id = ids[0];
        ArcheryCompetitions.Poll(id, players[0], 1020, 150, false, false);
        ArcheryCompetitions.Leave(id, players[0], 1021);
        foreach (var p in players.Skip(1)) ArcheryCompetitions.Poll(id, p, 1022, 5, true, false);
        var board = ArcheryCompetitions.Poll(id, players[1], 1023, 0, true, false)!.View;
        Assert.That(board.Finished, Is.True); Assert.That(board.Players.Length, Is.EqualTo(16));
        Assert.That(board.Players.Single(p => p.Id == players[0]).Rank, Is.Zero);
        Assert.That(board.Players.Where(p => !p.Forfeited).All(p => p.Rank == 1 && p.Score == 5), Is.True);
        foreach (var p in players) ArcheryCompetitions.Leave(id, p, 1024);
    }
    [Test]
    public void RealPlayersRegisterStartTogetherAndReleaseWorldHoldsOnWithdrawal()
    {
        using var world = new World(); world.Range.CompetitionEnabled = true; world.Range.MaximumPlayers = 2;
        var other = new Player { MapId = world.Map.Id, MapInstanceId = world.Player.MapInstanceId, InGame = true,
            X = 5, Y = 4, Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)) };
        GC.SuppressFinalize(other);
        try
        {
            world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.JoinCompetition, RangeId = world.Range.Id });
            var id = Competition(world.Player);
            Assert.That(id, Is.Not.EqualTo(Guid.Empty)); Assert.That(world.Player.CanUseLobbyTravel, Is.False);
            Assert.That(Match(world.Player), Is.Null);
            other.HandleArcheryRequest(new() { Action = ArcheryAction.JoinCompetition, RangeId = world.Range.Id });
            var second = Match(other)!; Tick(world.Player, second.Started + 250);
            Assert.That(Competition(other), Is.EqualTo(id)); Assert.That(Match(world.Player)!.Started, Is.EqualTo(second.Started));
            Assert.That(Match(world.Player)!.Wind, Is.EqualTo(second.Wind));
            other.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = Guid.NewGuid() });
            Assert.That(other.IsArcheryActive, Is.True);
            other.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = second.Id });
            Assert.That(other.IsArcheryActive, Is.False); Assert.That(other.CanUseLobbyTravel, Is.True);
            var board = ArcheryCompetitions.Poll(id, world.Player.Id, second.Started + 2, 0, false, false)!.View;
            Assert.That(board.Players.Single(p => p.Id == other.Id).Forfeited, Is.True);
            Tick(world.Player, second.Started + 11000);
            Assert.That(world.Player.IsArcheryActive, Is.False, "An expired participation lease must release world controls.");
            Assert.That(Match(world.Player)!.Phase, Is.EqualTo(ArcheryPhase.Abandoned));
        }
        finally
        {
            other.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = Competition(other) });
            other.Client = null;
        }
    }
    [Test]
    public void ClosingBeforeInitialSnapshotCancelsOnlyTheMatchingStartRequest()
    {
        using var world = new World(); world.Range.CompetitionEnabled = true;
        var token = Guid.NewGuid();
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.JoinCompetition, RangeId = world.Range.Id, RequestId = token });
        Assert.That(world.Player.IsArcheryActive, Is.True);
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = Guid.NewGuid() });
        Assert.That(world.Player.IsArcheryActive, Is.True);
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Leave, MatchId = token });
        Assert.That(world.Player.IsArcheryActive, Is.False); Assert.That(Competition(world.Player), Is.EqualTo(Guid.Empty));
    }
}
