using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Server.Dungeons;
using Intersect.Server.Core.MapInstancing;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using NUnit.Framework;

namespace Intersect.Tests.Server.Dungeons;

[TestFixture, NonParallelizable]
public sealed class DungeonReconnectTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type RunType = typeof(DungeonRunRuntime).GetNestedType("Run", BindingFlags.NonPublic)!;
    private static IDictionary Runs => (IDictionary)typeof(DungeonRunRuntime).GetField("RunsByInstance", Static)!.GetValue(null)!;
    private static ConcurrentDictionary<Guid, Player> Online => (ConcurrentDictionary<Guid, Player>)typeof(Player).GetField("OnlinePlayersById", Static)!.GetValue(null)!;

    private sealed class ReturningPlayer : Player
    {
        public int Warps;
        public override void Warp(Guid map, float x, float y, Direction direction, bool adminWarp = false,
            int zOverride = 0, bool mapSave = false, bool fromWarpEvent = false,
            MapInstanceType? mapInstanceType = null, bool fromLogin = false, bool forceInstanceChange = false)
        {
            Warps++; MapId = map; X = (int)x; Y = (int)y;
            if (mapInstanceType == MapInstanceType.Overworld) { MapInstanceId = Guid.Empty; InstanceType = MapInstanceType.Overworld; }
        }
    }

    private sealed class RunWorld : IDisposable
    {
        internal readonly Guid Instance = Guid.NewGuid();
        internal readonly MapController Map = new(Guid.NewGuid());
        internal readonly DungeonDefinition Dungeon;
        internal readonly object Run = Activator.CreateInstance(RunType, true)!;
        internal readonly ReturningPlayer First, Second;
        internal ConcurrentDictionary<Guid, byte> Participants => (ConcurrentDictionary<Guid, byte>)RunType.GetProperty("Participants")!.GetValue(Run)!;
        internal ConcurrentDictionary<Guid, byte> PartyMembers => (ConcurrentDictionary<Guid, byte>)RunType.GetProperty("PartyParticipants")!.GetValue(Run)!;
        internal Guid RunId => (Guid)RunType.GetProperty("Id")!.GetValue(Run)!;
        private readonly bool _oldKick;
        private readonly object? _oldConfiguration;
        internal RunWorld()
        {
            Options.EnsureCreated(); _oldKick = Options.Instance.Instancing.KickPartyMembersOnKick;
            Options.Instance.Instancing.KickPartyMembersOnKick = true;
            Dungeon = new() { MapIds = [Map.Id], ExitMapId = Guid.NewGuid(), CompletionRequirements = DungeonCompletionRequirement.None };
            MapController.Lookup.Set(Map.Id, Map); Map.SurroundingMaps = []; Map.SurroundingMapIds = [];
            RunType.GetProperty("Dungeon")!.SetValue(Run, Dungeon);
            RunType.GetProperty("EntryMapId")!.SetValue(Run, Map.Id);
            RunType.GetProperty("MapInstanceId")!.SetValue(Run, Instance);
            RunType.GetProperty("InstanceType")!.SetValue(Run, MapInstanceType.Shared);
            RunType.GetProperty("EndAtUnixMilliseconds")!.SetValue(Run, DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds());
            First = Create(Guid.NewGuid()); Second = Create(Guid.NewGuid());
            foreach (var player in new[] { First, Second }) { Participants[player.Id] = 0; PartyMembers[player.Id] = 0; Online[player.Id] = player; }
            First.Party = Second.Party = [First, Second]; Runs.Add(Instance, Run);
            InstanceProcessor.TryAddInstanceController(Instance, First);
            if (InstanceProcessor.TryGetInstanceController(Instance, out var controller)) controller.AddPlayer(Second);
            var config = typeof(DungeonConfigurationRuntime).GetField("_current", Static)!;
            _oldConfiguration = config.GetValue(null); config.SetValue(null, new DungeonConfiguration { Dungeons = [Dungeon] });
        }
        internal ReturningPlayer Create(Guid id)
        {
            var player = new ReturningPlayer { Id = id, InGame = true, MapId = Map.Id, MapInstanceId = Instance,
                InstanceType = MapInstanceType.Shared, SharedMapInstanceId = Instance, X = 5, Y = 6 };
            GC.SuppressFinalize(player); return player;
        }
        public void Dispose()
        {
            Runs.Remove(Instance);
            ((IDictionary)typeof(InstanceProcessor).GetField("InstanceControllers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Remove(Instance);
            Online.TryRemove(First.Id, out _); Online.TryRemove(Second.Id, out _);
            Options.Instance.Instancing.KickPartyMembersOnKick = _oldKick;
            typeof(DungeonConfigurationRuntime).GetField("_current", Static)!.SetValue(null, _oldConfiguration);
            MapController.Lookup.Delete(Map);
        }
    }

    [Test]
    public void DisconnectThenNewPlayerObjectRejoinsOriginalRunAndPartyAndCanAbandon()
    {
        using var world = new RunWorld();
        Assert.That(DungeonRunRuntime.PrepareDisconnect(world.First), Is.True);
        world.First.LeavePartyForDungeonDisconnect();
        Assert.That(world.First.Warps, Is.Zero, "Logging out must not trigger the shared-instance kick warp.");
        Assert.That(world.First.MapInstanceId, Is.EqualTo(world.Instance));
        Assert.That(world.PartyMembers.ContainsKey(world.First.Id), Is.True);
        InstanceProcessor.TryGetInstanceController(world.Instance, out var controller);
        controller.RemovePlayer(world.First.Id);
        var returning = world.Create(world.First.Id);
        returning.MapInstanceId = Guid.Empty; returning.SharedMapInstanceId = Guid.NewGuid();
        returning.Variables = world.First.Variables; Online[returning.Id] = returning;
        Assert.That(DungeonRunRuntime.RecoverLogin(returning), Is.True);
        Assert.That(returning.MapInstanceId, Is.EqualTo(world.Instance));
        Assert.That(returning.SharedMapInstanceId, Is.EqualTo(world.Instance));
        Assert.That(returning.Warps, Is.EqualTo(1));
        Assert.That(controller.Players, Does.Contain(returning), "The instance controller tracks the new connected player object.");
        Assert.That(returning.Party, Is.EquivalentTo(new Player[] { returning, world.Second }));
        Assert.That(world.Second.Party, Does.Contain(returning));
        Assert.That(world.Second.Party, Does.Not.Contain(world.First), "No stale disconnected player reference is restored.");
        Assert.That(DungeonRunRuntime.TryAbandon(returning, world.RunId), Is.True);
        Assert.That(returning.MapId, Is.EqualTo(world.Dungeon.ExitMapId));
        Assert.That(world.Participants.ContainsKey(returning.Id), Is.False);
        Assert.That(world.Participants.ContainsKey(world.Second.Id), Is.True);
    }

    [Test]
    public void AllMembersCanReconnectWithoutBeingAssignedNewSharedInstances()
    {
        using var world = new RunWorld();
        foreach (var player in new[] { world.First, world.Second })
        {
            Assert.That(DungeonRunRuntime.PrepareDisconnect(player), Is.True);
            player.LeavePartyForDungeonDisconnect(); Online.TryRemove(player.Id, out _);
        }
        var first = world.Create(world.First.Id); first.MapInstanceId = Guid.NewGuid(); Online[first.Id] = first;
        Assert.That(DungeonRunRuntime.RecoverLogin(first), Is.True);
        var second = world.Create(world.Second.Id); second.MapInstanceId = Guid.NewGuid(); Online[second.Id] = second;
        Assert.That(DungeonRunRuntime.RecoverLogin(second), Is.True);
        Assert.That(first.MapInstanceId, Is.EqualTo(world.Instance));
        Assert.That(second.MapInstanceId, Is.EqualTo(world.Instance));
        Assert.That(first.Party, Is.EquivalentTo(new Player[] { first, second }));
    }

    [Test]
    public void ExplicitPartyDepartureRevokesAutomaticPartyRejoinAndUnrelatedPartiesArePreserved()
    {
        using var world = new RunWorld();
        DungeonRunRuntime.OnPartyDeparture(world.First);
        Assert.That(world.PartyMembers.ContainsKey(world.First.Id), Is.False);
        var outsider = world.Create(Guid.NewGuid());
        world.First.Party = []; world.Second.Party = [world.Second, outsider];
        Assert.That(world.First.RestoreDungeonParty([world.Second]), Is.False);
        Assert.That(world.Second.Party, Does.Contain(outsider));
    }

    [Test]
    public void ActiveDungeonInstancesRetainDamagedEnemiesAcrossIdleCleanup()
    {
        using var world = new RunWorld();
        var instance = new MapInstance(world.Map, world.Instance, null!);
        typeof(MapInstance).GetField("mIsProcessing", Hidden)!.SetValue(instance, false);
        instance.LastRequestedUpdateTime = long.MaxValue;
        Assert.That(instance.ShouldBeActive(), Is.True);
        Assert.That(instance.ShouldBeCleaned(), Is.False);
        var descriptor = new NPCDescriptor(Guid.NewGuid()); descriptor.MaxVitals[(int)Vital.Health] = 100;
        var enemy = new Npc(descriptor) { MapId = world.Map.Id, MapInstanceId = world.Instance, MoveTimer = long.MaxValue, RegenTimer = long.MaxValue };
        GC.SuppressFinalize(enemy); enemy.SetVital(Vital.Health, 37); instance.AddEntity(enemy);
        typeof(MapInstance).GetMethod("UpdateEntities", Hidden)!.Invoke(instance, [Timing.Global.Milliseconds + Options.Instance.Map.TimeUntilMapCleanup + 1]);
        Assert.That(enemy.GetVital(Vital.Health), Is.EqualTo(37), "An idle dungeon must not regenerate damaged enemies on reconnect.");
        Runs.Remove(world.Instance);
        Assert.That(instance.ShouldBeActive(), Is.False);
    }
}
