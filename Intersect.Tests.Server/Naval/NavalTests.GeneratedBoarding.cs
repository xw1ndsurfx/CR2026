using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Database;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void AutomaticBoardingBuildsAnIsolatedPlayableDeckWithoutChangingAuthoredNpcs()
    {
        using var world = new EnemyTestWorld();
        var source = new NPCDescriptor(Guid.NewGuid()) { Name = "Herbonia sailor", Sprite = "crew.png", Level = 2 };
        NPCDescriptor.Lookup.Set(source.Id, source);
        MapController? deck = null;
        Guid crewId = default;
        try
        {
            var result = NavalSystem.CreateBoardingDeck(new HerboniaPatrol { Level = 10, BoardingCrewNpcId = source.Id, BoardingCrewCount = 4 }, 1000);
            Assert.That(result.HasValue, Is.True);
            var generated = result!.Value;
            deck = generated.Map;
            var layout = new NavalBoardingDeckLayout(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
            Assert.That(deck.GeneratedBoardingDeck && deck.IsIndoors, Is.True);
            Assert.That(generated.Dungeon.IsStructurallyValid, Is.True);
            Assert.That(generated.Dungeon.MinimumPartySize, Is.EqualTo(1));
            Assert.That(generated.Dungeon.NpcRespawnEnabled, Is.False);
            Assert.That(layout.Walkable(generated.X, generated.Y), Is.True);
            Assert.That(deck.Spawns.Count, Is.EqualTo(4));
            Assert.That(deck.Spawns.Select(spawn => (spawn.X, spawn.Y)).Distinct().Count(), Is.EqualTo(4));
            foreach (var spawn in deck.Spawns)
            {
                Assert.That(layout.Walkable(spawn.X, spawn.Y), Is.True);
                Assert.That(deck.Attributes[spawn.X, spawn.Y], Is.Null);
            }
            Assert.That(deck.Attributes[0, 0], Is.TypeOf<MapBlockedAttribute>());
            crewId = deck.Spawns[0].NpcId;
            var crew = NPCDescriptor.Get(crewId)!;
            Assert.That(crew.Level, Is.EqualTo(10));
            Assert.That(crew.Damage, Is.EqualTo(20));
            Assert.That(crew.MaxVitals[(int)Vital.Health], Is.EqualTo(160));
            Assert.That(source.Level, Is.EqualTo(2));
            Assert.That(crew.Id, Is.Not.EqualTo(source.Id));
            var index = deck.MapGrid;
            Assert.That(index, Is.LessThan(0));
            var grid = DbInterface.GetGrid(index);
            Assert.That(grid.MapIds, Is.EquivalentTo(new[] { deck.Id }));
            Assert.That(grid.MapIdGrid[0, 0], Is.EqualTo(deck.Id));
            Assert.That(deck.MapGridX, Is.Zero);
            Assert.That(deck.MapGridY, Is.Zero);
            world.Invoke("PruneGeneratedBoardingDecks", 31001L);
            Assert.That(MapController.Get(deck.Id), Is.Null);
            Assert.That(NPCDescriptor.Get(crewId), Is.Null);
            Assert.That(NPCDescriptor.Get(source.Id), Is.SameAs(source));
            Assert.Throws<KeyNotFoundException>(() => DbInterface.GetGrid(index));
            deck = null;
        }
        finally
        {
            if (deck != null)
            {
                world.Invoke("PruneGeneratedBoardingDecks", long.MaxValue);
            }
            NPCDescriptor.Lookup.DeleteAt(source.Id);
        }
    }

    [Test]
    public void UnconfiguredPatrolCanBeBoardedSoloAndCeasesFireOnlyNearADisabledHull()
    {
        using var world = new EnemyTestWorld();
        var ship = world.Add(10.5, 10.5, false);
        var enemy = world.Add(13.5, 10.5, true);
        world.Set(ship, "OwnerId", world.State(ship).CaptainId);
        var patrol = world.Field<HerboniaPatrol>(enemy, "Enemy");
        world.Set(enemy, "Health", 25);
        Assert.That(world.Invoke("CanBoardEnemy", ship, enemy), Is.True);
        Assert.That(world.Invoke("BoardingCeasefire", enemy), Is.True);
        patrol.AutomaticBoardingDeck = false;
        Assert.That(world.Invoke("CanBoardEnemy", ship, enemy), Is.False);
        Assert.That(world.Invoke("BoardingCeasefire", enemy), Is.False);
        patrol.AutomaticBoardingDeck = true;
        world.State(ship).X = 30;
        Assert.That(world.Invoke("BoardingCeasefire", enemy), Is.False);
        world.State(ship).X = 10.5;
        world.Set(enemy, "Health", 26);
        Assert.That(world.Invoke("BoardingCeasefire", enemy), Is.False);
    }
}
