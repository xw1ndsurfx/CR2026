using System.Runtime.CompilerServices;
using Intersect.Network;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using MessagePack;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class WorldMapExplorationTests
{
    private Guid _first, _second;

    [SetUp]
    public void SetUp()
    {
        Options.EnsureCreated();
        _first = Guid.NewGuid(); _second = Guid.NewGuid();
        MapController.Lookup.Set(_first, new MapController(_first));
        MapController.Lookup.Set(_second, new MapController(_second));
    }

    [TearDown]
    public void TearDown()
    {
        MapController.Lookup.DeleteAt(_first); MapController.Lookup.DeleteAt(_second);
    }

    private static Player NewPlayer(Guid map, Guid? id = null)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        player.Id = id ?? Guid.NewGuid(); player.MapId = map;
        player.EntityLock = new(); player.Variables = [];
        return player;
    }

    [Test]
    public void OnlyAuthoritativeArrivalDiscoversAndRepeatedVisitsAreIdempotent()
    {
        var player = NewPlayer(_first);
        Assert.That(player.WorldMapExplorationSnapshot().DiscoveredMapIds, Is.Empty,
            "Reading the atlas is not a visit, even for maps in the current grid.");
        Assert.That(player.DiscoverWorldMap(_second), Is.False);
        Assert.That(player.DiscoverWorldMap(Guid.Empty), Is.False);
        Assert.That(player.DiscoverWorldMap(_first), Is.True);
        var json = player.Variables.Single().Value.String;
        Assert.That(player.DiscoverWorldMap(_first), Is.False);
        Assert.That(player.Variables.Single().Value.String, Is.EqualTo(json));
        Assert.That(player.HasDiscoveredWorldMap(_second), Is.False);
        player.MapId = _second;
        Assert.That(player.DiscoverWorldMap(_second), Is.True);
        Assert.That(player.WorldMapExplorationSnapshot().DiscoveredMapIds, Is.EquivalentTo(new[] { _first, _second }));
        Assert.That(player.Variables, Has.Count.EqualTo(1));
        player.MapId = Guid.NewGuid();
        Assert.That(player.DiscoverWorldMap(player.MapId), Is.False, "Missing map cannot be discovered.");
    }

    [Test]
    public void ConcurrentVisitsHaveOnePersistentEntryAndSnapshotsCannotMutateProgress()
    {
        var player = NewPlayer(_first);
        var discoveries = 0;
        Parallel.For(0, 32, _ => { if (player.DiscoverWorldMap(_first)) Interlocked.Increment(ref discoveries); });
        Assert.That(discoveries, Is.EqualTo(1));
        Assert.That(player.Variables, Has.Count.EqualTo(1));
        player.WorldMapExplorationSnapshot().DiscoveredMapIds[0] = _second;
        Assert.That(player.HasDiscoveredWorldMap(_first), Is.True);
        Assert.That(player.HasDiscoveredWorldMap(_second), Is.False);
    }

    [TestCase("")]
    [TestCase("null")]
    [TestCase("{broken")]
    [TestCase("[\"not-a-guid\"]")]
    [TestCase("[\"00000000-0000-0000-0000-000000000000\"]")]
    public void LegacyOrDamagedProgressStartsHiddenAndCanDiscoverCurrentMap(string json)
    {
        var player = NewPlayer(_first);
        player.Variables.Add(new(Player.WorldMapExplorationId) { Value = new() { Value = json } });
        Assert.That(player.WorldMapExplorationSnapshot().DiscoveredMapIds, Is.Empty);
        Assert.That(player.DiscoverWorldMap(_first), Is.True);
        Assert.That(player.WorldMapExplorationSnapshot().DiscoveredMapIds, Is.EqualTo(new[] { _first }));
    }

    private sealed class VariableContext(string path) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Ignore<Player>();
            model.Entity<PlayerVariable>().Ignore(variable => variable.Player);
            model.Entity<PlayerVariable>().ToTable("Player_Variables");
            model.Entity<PlayerVariable>().HasIndex(v => new { v.VariableId, v.PlayerId }).IsUnique();
        }
    }

    [Test]
    public void NativeVariableSurvivesSqliteReloadAndCharactersRemainIndependent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"worldmap-exploration-{Guid.NewGuid():N}.db");
        try
        {
            var first = NewPlayer(_first); var second = NewPlayer(_second);
            first.DiscoverWorldMap(_first); first.MapId = _second; first.DiscoverWorldMap(_second);
            second.DiscoverWorldMap(_second);
            using (var db = new VariableContext(path))
            {
                db.Database.EnsureCreated();
                foreach (var player in new[] { first, second })
                    foreach (var variable in player.Variables)
                        db.Add(variable).Property(v => v.PlayerId).CurrentValue = player.Id;
                db.SaveChanges();
            }
            using (var db = new VariableContext(path))
            {
                var returning = NewPlayer(_second, first.Id);
                returning.Variables = db.Set<PlayerVariable>().AsNoTracking().Where(v => v.PlayerId == first.Id).ToList();
                var other = NewPlayer(_second, second.Id);
                other.Variables = db.Set<PlayerVariable>().AsNoTracking().Where(v => v.PlayerId == second.Id).ToList();
                Assert.That(returning.WorldMapExplorationSnapshot().DiscoveredMapIds, Is.EquivalentTo(new[] { _first, _second }));
                Assert.That(other.HasDiscoveredWorldMap(_first), Is.False);
                Assert.That(other.HasDiscoveredWorldMap(_second), Is.True);
                Assert.That(returning.DiscoverWorldMap(_second), Is.False);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void ExplorationPacketRoundTripsCharacterAndMaps()
    {
        var packet = new WorldMapExplorationPacket { PlayerId = Guid.NewGuid(), DiscoveredMapIds = [_first, _second] };
        var copy = MessagePackSerializer.Deserialize<WorldMapExplorationPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.PlayerId, Is.EqualTo(packet.PlayerId));
        Assert.That(copy.DiscoveredMapIds, Is.EqualTo(packet.DiscoveredMapIds));

        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True);
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        var received = MessagePacker.Instance.Deserialize<WorldMapExplorationPacket>(MessagePacker.Instance.Serialize(packet));
        Assert.That(received, Is.Not.Null);
        Assert.That(received!.PlayerId, Is.EqualTo(packet.PlayerId));
        Assert.That(received.DiscoveredMapIds, Is.EqualTo(packet.DiscoveredMapIds));
    }
}
