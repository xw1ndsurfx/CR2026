using System.Collections;
using System.Reflection;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.WorldEvents.Invasions;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class InvasionSimulationTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

    private sealed class Fixture : IDisposable
    {
        internal readonly MapController Spawn = new(Guid.NewGuid()), Route = new(Guid.NewGuid()), Goal = new(Guid.NewGuid());
        internal readonly object Session;
        internal readonly Type SessionType = typeof(InvasionRuntime).GetNestedType("Session", BindingFlags.NonPublic)!;
        internal readonly Guid SessionId;
        private readonly IDictionary _sessions;
        internal readonly IList Targets;
        internal readonly IDictionary Npcs;
        internal Fixture()
        {
            Options.EnsureCreated();
            Spawn.Right = Route.Id; Route.Left = Spawn.Id; Route.Right = Goal.Id; Goal.Left = Route.Id;
            foreach (var map in new[] { Spawn, Route, Goal }) MapController.Lookup.Set(map.Id, map);
            var definition = new InvasionDefinition { TargetMapId = Goal.Id, TargetX = 10, TargetY = 10, TargetHealth = 20,
                ObjectiveHitIntervalMs = 100, Waves = [new() { Spawns = [new() { SpawnMapId = Spawn.Id }] }] };
            Session = Activator.CreateInstance(SessionType, true)!;
            SessionType.GetProperty("Definition")!.SetValue(Session, definition);
            Targets = (IList)typeof(InvasionRuntime).GetMethod("CreateDefenseTargets", Static)!.Invoke(null, [definition])!;
            SessionType.GetProperty("Objectives")!.SetValue(Session, Targets);
            SessionType.GetProperty("LastStatusAtMs")!.SetValue(Session, 10000L);
            Npcs = (IDictionary)SessionType.GetProperty("ActiveNpcs")!.GetValue(Session)!;
            SessionId = (Guid)SessionType.GetProperty("SessionId")!.GetValue(Session)!;
            _sessions = (IDictionary)typeof(InvasionRuntime).GetField("SessionsById", Static)!.GetValue(null)!;
            _sessions.Add(SessionId, Session);
        }
        internal void Refresh() => typeof(InvasionRuntime).GetMethod("RefreshSimulationMaps", Static)!.Invoke(null, null);
        internal void Update(long now) => typeof(InvasionRuntime).GetMethod("UpdateSession", Static)!.Invoke(null, [Session, now]);
        public void Dispose()
        {
            _sessions.Remove(SessionId); Refresh();
            foreach (var map in new[] { Spawn, Route, Goal }) MapController.Lookup.Delete(map);
        }
    }

    private sealed class CountingNpc : Npc
    {
        internal int Updates;
        internal CountingNpc() : base(new NPCDescriptor(Guid.NewGuid())) { }
        public override void Update(long timeMs) => Updates++;
    }

    [Test]
    public void InvasionMapsContinueEntityUpdatesWithoutPlayersAndReleaseWhenFinished()
    {
        Options.EnsureCreated();
        using var world = new Fixture();
        world.Refresh();
        Assert.That(InvasionRuntime.SimulationMapIds, Does.Contain(world.Route.Id), "Intermediate maps must keep border-crossing NPCs active.");
        var map = new MapInstance(world.Spawn, MapInstance.OverworldInstanceId, null!);
        var npc = new CountingNpc { MapId = world.Spawn.Id, MapInstanceId = MapInstance.OverworldInstanceId,
            InvasionSessionId = world.SessionId };
        npc.SetMaxVital(Vital.Health, 100); npc.SetVital(Vital.Health, 7);
        map.AddEntity(npc);
        typeof(MapInstance).GetField("mIsProcessing", Private)!.SetValue(map, false);
        var now = Options.Instance.Map.TimeUntilMapCleanup + 10000L;
        map.Update(now); map.Update(now + 1000);
        Assert.That(npc.Updates, Is.EqualTo(2), "No connected players are needed to advance an active invasion.");
        Assert.That(npc.GetVital(Vital.Health), Is.EqualTo(7), "Leaving a map must not heal invaders.");
        Assert.That(map.ShouldBeActive(), Is.True);
        var privateMap = new MapInstance(world.Spawn, Guid.NewGuid(), null!);
        Assert.That((bool)typeof(MapInstance).GetProperty("InvasionNeedsSimulation", Private)!.GetValue(privateMap)!, Is.False,
            "Overworld invasions must not keep dungeon instances active.");
        world.SessionType.GetProperty("Completed")!.SetValue(world.Session, true); world.Refresh();
        Assert.That(InvasionRuntime.RequiresSimulation(world.Spawn.Id), Is.False);
        map.Update(now + 2000); map.Update(now + 3000);
        Assert.That(npc.Updates, Is.EqualTo(3), "Normal map idling resumes once the invasion ends.");
    }

    [Test]
    public void DeadDisconnectedOrDifferentInstanceDefendersCannotBlockObjectiveDamage()
    {
        using var world = new Fixture();
        var npc = new Npc(new NPCDescriptor(Guid.NewGuid())) { MapId = world.Goal.Id,
            MapInstanceId = MapInstance.OverworldInstanceId, X = 10, Y = 9, InvasionSessionId = world.SessionId, InvasionObjectiveDamage = 3 };
        world.Npcs.Add(npc.Id, npc);
        var defender = new Player { InGame = true, MapId = world.Goal.Id, MapInstanceId = npc.MapInstanceId, X = 10, Y = 9 };
        npc.Target = defender;
        Assert.That(npc.HasLiveInvasionCombatTarget(defender), Is.True);
        world.Update(10000);
        Assert.That(world.Targets[0]!.GetType().GetProperty("Health")!.GetValue(world.Targets[0]), Is.EqualTo(20), "A live defender can engage an invader.");
        defender.IsDead = true;
        world.Update(10100);
        defender.IsDead = false; defender.InGame = false;
        world.Update(10200);
        defender.InGame = true; defender.MapInstanceId = Guid.NewGuid();
        world.Update(10300);
        Assert.That(world.Targets[0]!.GetType().GetProperty("Health")!.GetValue(world.Targets[0]), Is.EqualTo(11),
            "Siege damage continues after death, disconnect or instance change, even before the NPC retarget tick.");
    }
}
