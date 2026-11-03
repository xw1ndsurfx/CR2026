using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Server.Database;
using NUnit.Framework;

namespace Intersect.Tests.Server;
public sealed partial class RoyalCombatTests
{
    [Test]
    public void AuthoredRangeRulesAreSnapshottedAndServerRejectsStaleRoundActions()
    {
        using var world = new World();
        world.Range.Rules = new() { Rounds = [new() { Distance = 25, Arrows = 2, Seconds = 15,
            Wind = 1.5, Targets = [new(), new() { MotionX = 1 }] }] };
        world.Start(); var match = Match(world.Player)!;
        Assert.That(match.Distance, Is.EqualTo(25)); Assert.That(match.TotalArrows, Is.EqualTo(2));
        Assert.That(match.Wind, Is.EqualTo(1.5));
        world.Range.Rules.Rounds[0].Distance = 100;
        world.Range.Rules.Rounds[0].Targets[0].Radius = 1.5;
        Assert.That(match.Distance, Is.EqualTo(25)); Assert.That(match.RoundSettings.Targets[0].Radius, Is.EqualTo(.6));
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Draw, MatchId = match.Id, Sequence = 1, Round = 2, Shot = 0 });
        Assert.That(match.Phase, Is.EqualTo(ArcheryPhase.Ready), "A client cannot pick the next round.");
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Draw, MatchId = match.Id, Sequence = 2, Round = 1, Shot = 0 });
        Assert.That(match.Phase, Is.EqualTo(ArcheryPhase.Drawing));
        Tick(world.Player, match.RoundDeadline);
        Assert.That(match.Phase, Is.EqualTo(ArcheryPhase.Finished));
        Assert.That(match.Impacts.Count, Is.EqualTo(2)); Assert.That(match.Impacts.All(h => h.TimedOut), Is.True);
        Assert.That(world.Player.IsArcheryActive, Is.False); Assert.That(world.Player.CanUseLobbyTravel, Is.True);
        world.Player.HandleArcheryRequest(new() { Action = ArcheryAction.Release, MatchId = match.Id, Sequence = 3, Round = 1, Shot = 0 });
        Assert.That(match.Score, Is.Zero); Assert.That(match.Impacts.Count, Is.EqualTo(2));
    }
    [Test]
    public void TrainingTimeoutDoesNotSpendExistingInventoryAmmunition()
    {
        using var world = new World();
        var arrow = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Ammo, Stackable = true };
        ItemDescriptor.Lookup.Set(arrow.Id, arrow);
        try
        {
            world.Player.Items[0].Set(new Item(arrow.Id, 25));
            world.Start(); var match = Match(world.Player)!;
            Tick(world.Player, match.Started + 300000);
            Assert.That(match.Done, Is.True); Assert.That(match.TotalArrows, Is.EqualTo(15));
            Assert.That(world.Player.FindInventoryItemQuantity(arrow.Id), Is.EqualTo(25));
        }
        finally { ItemDescriptor.Lookup.DeleteAt(arrow.Id); }
    }
}
