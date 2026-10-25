using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Jousting;
using Intersect.Server.Maps;
using Intersect.Server.Mounts;
using Intersect.Server.Networking;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server;
[TestFixture, NonParallelizable]
public sealed class JoustWorldTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object? Get(object owner, string name) => owner.GetType().GetField(name, Hidden)!.GetValue(owner);
    private static void Tick(Player owner, long now) => typeof(Player).GetMethod("UpdateJoust", Hidden)!.Invoke(owner, [(long?)now]);
    private sealed class World : IDisposable
    {
        internal readonly MapController Map;
        internal readonly MapInstance Instance;
        internal readonly Player Owner;
        internal readonly JoustArena Arena;
        private readonly object? _mounts, _arenas;
        private static readonly FieldInfo MountCache = typeof(MountRuntime).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly FieldInfo ArenaCache = typeof(JoustRuntime).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        internal World()
        {
            Options.EnsureCreated(); _mounts = MountCache.GetValue(null); _arenas = ArenaCache.GetValue(null);
            Map = new MapController(Guid.NewGuid()); MapController.Lookup.Set(Map.Id, Map);
            Owner = new Player { MapId = Map.Id, MapInstanceId = MapInstance.OverworldInstanceId, InGame = true,
                Client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)), X = 4, Y = 14 };
            GC.SuppressFinalize(Owner); Owner.BaseStats[(int)Stat.Speed] = 20;
            Instance = new MapInstance(Map, MapInstance.OverworldInstanceId, Owner);
            var instances = (ConcurrentDictionary<Guid, MapInstance>)Get(Map, "mInstances")!;
            instances[MapInstance.OverworldInstanceId] = Instance;
            typeof(MapController).GetField("mSurroundingMapsWithSelf", Hidden)!.SetValue(Map, new[] { Map });
            Instance.CacheMapBlocks(); Instance.AddEntity(Owner);
            var mount = MountConfiguration.Prototype().Mounts[0]; mount.IntegratedRider = true; mount.Starter = false;
            MountCache.SetValue(null, new MountConfiguration { Mounts = [mount] });
            Arena = new() { MapId = Map.Id, Enabled = true, MountId = mount.Id };
            Arena.Rules.UnhorseEndsMatch = false;
            ArenaCache.SetValue(null, new JoustConfiguration { Arenas = [Arena] });
            Owner.HandleJoustRequest(new() { Action = JoustAction.StartPve, ArenaId = Arena.Id, Difficulty = 2 });
            Assert.That(Match, Is.Not.Null, "Start request must reach the real server session handler.");
        }
        internal JoustMatch Match => (JoustMatch)Get(Owner, "_joust")!;
        internal JoustOpponent? Opponent => (JoustOpponent?)Get(Owner, "_joustOpponent");
        public void Dispose()
        {
            Owner.HandleJoustRequest(new() { Action = JoustAction.Leave, MatchId = Match.Id });
            Instance.RemoveEntity(Owner); Owner.Client = null;
            MapController.Lookup.Delete(Map); MountCache.SetValue(null, _mounts); ArenaCache.SetValue(null, _arenas);
        }
    }
    [Test]
    public void ThreePassWorldDuelMovesBothLanesThenRemovesActorAndRestoresMount()
    {
        using var world = new World();
        var actor = world.Opponent!; var began = world.Match.PhaseStarted;
        Assert.That(world.Instance.GetEntities(), Does.Contain(actor), "Actor is in the shared map, not a personal HUD image.");
        Assert.That(world.Owner.CanUseLobbyTravel, Is.False, "Lobby travel cannot pull a knight out of a live charge.");
        var packet = MessagePackSerializer.Deserialize<NpcEntityPacket>(MessagePackSerializer.Serialize((NpcEntityPacket)actor.EntityPacket()));
        Assert.That(MountConfiguration.FromJson(packet.MountJson).Mounts.Single().IntegratedRider, Is.True);
        for (var t = began; t < began + 60000 && !world.Match.Done; t += 50)
        {
            Tick(world.Owner, t);
            if (world.Match.Done) break;
            if (world.Match.Phase == JoustPhase.Preparation)
            {
                Assert.That(world.Owner.ScriptedMovementMilliseconds, Is.Zero);
                Assert.That(actor.StepMilliseconds, Is.Zero, "Preparation ends scripted gallop for both observers and the participant.");
            }
            Assert.Multiple(() => {
                Assert.That(world.Owner.X + actor.X, Is.EqualTo(world.Arena.StartX + world.Arena.EndX));
                Assert.That(world.Owner.Y, Is.EqualTo(world.Arena.StartY));
                Assert.That(actor.Y, Is.EqualTo(world.Arena.OpponentY));
            });
        }
        Assert.Multiple(() => {
            Assert.That(world.Match.Phase, Is.EqualTo(JoustPhase.Finished));
            Assert.That(world.Match.Pass, Is.EqualTo(3));
            Assert.That(world.Owner.X, Is.EqualTo(world.Arena.EndX));
            Assert.That(world.Opponent, Is.Null);
            Assert.That(actor.IsDisposed, Is.True);
            Assert.That(world.Instance.GetEntities(), Does.Not.Contain(actor));
            Assert.That(world.Owner.ActiveMountId, Is.EqualTo(Guid.Empty));
            Assert.That(world.Owner.ScriptedMovementMilliseconds, Is.Zero);
        });
        Assert.That(JoustRuntime.Reserve(world.Arena.Id, Guid.Empty), Is.True);
        JoustRuntime.Release(world.Arena.Id, Guid.Empty);
    }
    [TestCase("disconnect")]
    [TestCase("map")]
    [TestCase("instance")]
    [TestCase("death")]
    [TestCase("obstacle")]
    [TestCase("server-stall")]
    public void InterruptedWorldDuelCleansUpItsActorAndLease(string reason)
    {
        using var world = new World(); var actor = world.Opponent!; var began = world.Match.PhaseStarted;
        switch (reason)
        {
            case "disconnect": world.Owner.Client = null; break;
            case "map": world.Owner.MapId = Guid.NewGuid(); break;
            case "instance": world.Owner.MapInstanceId = Guid.NewGuid(); break;
            case "death": world.Owner.IsDead = true; break;
            case "obstacle": world.Map.Attributes[5, world.Arena.StartY] = new MapBlockedAttribute(); break;
        }
        Tick(world.Owner, began + world.Arena.Rules.PreparationMs + (reason == "server-stall" ? 1000 : 0));
        Assert.That(world.Match.Phase, Is.EqualTo(JoustPhase.Abandoned));
        Assert.That(actor.IsDisposed, Is.True); Assert.That(world.Instance.GetEntities(), Does.Not.Contain(actor));
        Assert.That(JoustRuntime.Reserve(world.Arena.Id, Guid.Empty), Is.True); JoustRuntime.Release(world.Arena.Id, Guid.Empty);
    }
    [Test]
    public void ScriptedDurationIsOptionalAndSurvivesNativeMovementSerialization()
    {
        var packet = new EntityMovePacket(Guid.NewGuid(), EntityType.GlobalEntity, Guid.NewGuid(), 4, 5, 3, true, 564);
        Assert.That(MessagePackSerializer.Deserialize<EntityMovePacket>(MessagePackSerializer.Serialize(packet)).ScriptedDurationMs, Is.EqualTo(564));
        Assert.That(new EntityMovePacket().ScriptedDurationMs, Is.Zero);
        Assert.That(new NpcEntityPacket().MountJson, Is.Empty);
    }
}
