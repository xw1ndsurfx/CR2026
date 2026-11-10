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
    public void BoardingOnRealShipsUsesAllSevenAuthoredAppearancesAndCleansUpOnlyTheirClones()
    {
        using var world=new EnemyTestWorld();
        var sources=Enumerable.Range(0,7).Select(i=>new NPCDescriptor(Guid.NewGuid()){Name="Deck crew "+i,Sprite="crew-"+i+".png",Level=2}).ToArray();
        MapController? deck=null;
        try
        {
            foreach(var source in sources)NPCDescriptor.Lookup.Set(source.Id,source);
            var patrol=new HerboniaPatrol {Level=12,DeckCaptainNpcId=sources[0].Id,
                PortGunner1NpcId=sources[1].Id,PortGunner2NpcId=sources[2].Id,PortGunner3NpcId=sources[3].Id,
                StarboardGunner1NpcId=sources[4].Id,StarboardGunner2NpcId=sources[5].Id,StarboardGunner3NpcId=sources[6].Id};
            var generated=NavalSystem.CreateBoardingDeckOnShips(patrol,new(){Graphics=NavalShipModels.ThreeMasts[0].Graphics},1000);
            Assert.That(generated.HasValue,Is.True);deck=generated!.Value.Map;
            Assert.That(deck.BoardingScene,Is.Not.Null);Assert.That(deck.Spawns,Has.Count.EqualTo(7));
            var cloneIds=deck.Spawns.Select(s=>s.NpcId).ToArray();
            Assert.That(cloneIds.Distinct().Count(),Is.EqualTo(7));
            for(var i=0;i<7;i++)
            {
                var clone=NPCDescriptor.Get(cloneIds[i])!;
                Assert.That(clone.Sprite,Is.EqualTo(sources[i].Sprite));Assert.That(clone.Level,Is.EqualTo(12));
                Assert.That(clone.Id,Is.Not.EqualTo(sources[i].Id));Assert.That(sources[i].Level,Is.EqualTo(2));
                Assert.That(deck.BoardingScene!.Walkable(deck.Spawns[i].X,deck.Spawns[i].Y),Is.True);
            }
            world.Invoke("PruneGeneratedBoardingDecks",31001L);
            Assert.That(MapController.Get(deck.Id),Is.Null);
            foreach(var clone in cloneIds)Assert.That(NPCDescriptor.Get(clone),Is.Null);
            foreach(var source in sources)Assert.That(NPCDescriptor.Get(source.Id),Is.SameAs(source));
        }
        finally
        {
            world.Invoke("PruneGeneratedBoardingDecks",long.MaxValue);
            foreach(var source in sources)NPCDescriptor.Lookup.DeleteAt(source.Id);
        }
    }
    private sealed class BoardingTestPlayer : Intersect.Server.Entities.Player
    {
        public override long GetMaxVital(int vital) => vital == (int)Vital.Health ? 100000 : base.GetMaxVital(vital);
    }

    [Test]
    public void AutomaticBoardingBuildsAnIsolatedPlayableDeckWithoutChangingAuthoredNpcs()
    {
        using var world = new EnemyTestWorld();
        var formulas = typeof(Intersect.Server.General.Formulas).GetField("_formulas", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var previousFormulas = formulas.GetValue(null);
        formulas.SetValue(null, new Intersect.Server.General.Formulas { PhysicalDamage = "BaseDamage", MagicDamage = "BaseDamage", TrueDamage = "BaseDamage" });
        var source = new NPCDescriptor(Guid.NewGuid()) { Name = "Herbonia sailor", Sprite = "crew.png", Level = 2, IsPet = true, Movement = (byte)NpcMovement.Static };
        NPCDescriptor.Lookup.Set(source.Id, source);
        MapController? deck = null;
        Guid crewId = default;
        try
        {
            var result = NavalSystem.CreateBoardingDeck(new HerboniaPatrol { Level = 10, BoardingCrewNpcId = source.Id, BoardingCrewCount = 12 }, 1000);
            Assert.That(result.HasValue, Is.True);
            var generated = result!.Value;
            deck = generated.Map;
            var layout = new NavalBoardingDeckLayout(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
            Assert.That(deck.GeneratedBoardingDeck && deck.IsIndoors, Is.True);
            Assert.That(generated.Dungeon.IsStructurallyValid, Is.True);
            Assert.That(generated.Dungeon.MinimumPartySize, Is.EqualTo(1));
            Assert.That(generated.Dungeon.NpcRespawnEnabled, Is.False);
            Assert.That(layout.Walkable(generated.X, generated.Y), Is.True);
            Assert.That(deck.Spawns.Count, Is.EqualTo(12));
            Assert.That(deck.Spawns.Select(spawn => (spawn.X, spawn.Y)).Distinct().Count(), Is.EqualTo(12));
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
            Assert.That(crew.Movement, Is.Not.EqualTo((byte)NpcMovement.Static), "Generated sailors must be able to pursue boarders.");
            Assert.That(crew.Aggressive, Is.True);
            var instanceId = Guid.NewGuid();
            var instance = new MapInstance(deck, instanceId, null!);
            var instances = (System.Collections.IDictionary)typeof(MapController)
                .GetField("mInstances", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(deck)!;
            instances.Add(instanceId, instance);
            var boarder = new BoardingTestPlayer { Id = Guid.NewGuid(), MapId = deck.Id,
                MapInstanceId = instanceId, InGame = true, X = (byte)generated.X, Y = (byte)generated.Y };
            GC.SuppressFinalize(boarder);
            boarder.SetMaxVital(Vital.Health, 100000); boarder.SetVital(Vital.Health, 100000);
            instance.AddEntity(boarder);
            instance.CacheMapBlocks();
            try
            {
                foreach (var spawn in deck.Spawns)
                {
                    var sailor = new Intersect.Server.Entities.Npc(crew) { MapId = deck.Id, MapInstanceId = instanceId,
                        X = (byte)spawn.X, Y = (byte)spawn.Y };
                    GC.SuppressFinalize(sailor); sailor.ConfigureBoardingCombat(); instance.AddEntity(sailor);
                    try
                    {
                        sailor.TryFindNewTarget(long.MaxValue);
                        Assert.That(sailor.Target, Is.SameAs(boarder), "Every sailor detects the player at the boarding entry.");
                        var before = (sailor.X, sailor.Y);
                        for (var tick = 0; tick < 100 && (Math.Abs(sailor.X - boarder.X) + Math.Abs(sailor.Y - boarder.Y) > 1); tick++)
                        {
                            sailor.MoveTimer = 0;
                            var finder = typeof(Intersect.Server.Entities.Npc).GetField("mPathFinder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(sailor)!;
                            finder.GetType().GetField("mWaitTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(finder, 0L);
                            sailor.Update(Timing.Global.Milliseconds + 1);
                        }
                        sailor.AttackTimer = 0; sailor.Dir = sailor.GetDirectionTo(boarder);
                        sailor.TryAttack(boarder);
                        Assert.That(sailor.GetDistanceTo(boarder), Is.LessThanOrEqualTo(1), "Crew reaches melee range through real update ticks.");
                        Assert.That((sailor.X, sailor.Y), Is.Not.EqualTo(before), "Actual combat ticks pursue the boarder.");

                    }
                    finally { instance.RemoveEntity(sailor); }
                }
            }
            finally { instance.RemoveEntity(boarder); }
            Assert.That(boarder.GetVital(Vital.Health), Is.LessThan(100000), "Crew attacks and damages the boarder at melee range.");
            var passiveCrew = new Intersect.Server.Entities.Npc(source) { MapId = deck.Id, MapInstanceId = instanceId, X = (byte)layout.EntryX, Y = (byte)(layout.EntryY - 1) };
            GC.SuppressFinalize(passiveCrew);
            passiveCrew.ConfigureBoardingCombat();
            Assert.That(passiveCrew.ShouldAttackPlayerOnSight(boarder), Is.True, "A passive source NPC becomes hostile only for the boarding encounter.");
            Assert.That(passiveCrew.CanMoveInDirection(Direction.Up, out _, out _), Is.True, "A static source NPC can pursue boarders after runtime configuration.");
            var companion = instance.SpawnNpc((byte)layout.EntryX, (byte)(layout.EntryY - 1), Direction.Down, source.Id,
                configure: pet => pet.PetOwner = boarder);
            Assert.That(companion.ShouldAttackPlayerOnSight(boarder), Is.False, "An actually owned companion remains friendly on the boarding deck.");
            instance.RemoveEntity(companion);
            passiveCrew.ConfigureBoardingCombat(false);
            Assert.That(passiveCrew.ShouldAttackPlayerOnSight(boarder), Is.False, "Finishing the encounter restores authored aggression.");
            Assert.That(passiveCrew.CanMoveInDirection(Direction.Up, out _, out _), Is.False, "Finishing the encounter restores authored static movement.");
            Assert.That(source.Level, Is.EqualTo(2));
            Assert.That(source.Movement, Is.EqualTo((byte)NpcMovement.Static), "The authored NPC keeps its original movement behavior.");
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
            formulas.SetValue(null, previousFormulas);
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
