using System.Reflection;
using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.MountRacing;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.MountRacing;
using Intersect.Server.Mounts;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class MountRaceTests
{
    private string _folder = "";
    private readonly List<ItemDescriptor> _items = [];
    [SetUp] public void Setup() { Options.EnsureCreated(); _folder = Path.Combine(Path.GetTempPath(), "mount-race-test-" + Guid.NewGuid()); Directory.CreateDirectory(_folder); }
    [TearDown] public void Cleanup() { foreach (var item in _items) ItemDescriptor.Lookup.Delete(item); _items.Clear(); Directory.Delete(_folder, true); }
    private static MountRaceCourse Course() => new() { Enabled = true, MapId = Guid.NewGuid(), RegistrationMapId = Guid.NewGuid(), StartX = 4, StartY = 4,
        Laps = 2, Checkpoints = [new() { X = 6, Y = 4 }, new() { X = 6, Y = 6 }, new() { X = 4, Y = 6 }, new() { X = 4, Y = 4 }] };
    private static MountRaceRound Round(bool practice = false)
    { var c = Course(); return new(c, Enumerable.Range(0, practice ? 1 : 2).Select(i => (Guid.NewGuid(), "Rider " + i)), practice, 1000); }
    private static long Start(MountRaceRound round)
    { foreach (var r in round.Riders) Assert.That(round.Confirm(r.Id, round.Id, 1100), Is.True); Assert.That(round.Begin(1200), Is.True); round.Tick(round.Deadline); return round.StartedAt; }
    private static long Lap(MountRaceRound round, RaceRider r, long now)
    {
        foreach (var target in round.Course.Checkpoints)
            while (r.X != target.X || r.Y != target.Y)
            { var x = r.X + Math.Sign(target.X - r.X); var y = r.Y + (x == r.X ? Math.Sign(target.Y - r.Y) : 0); Assert.That(round.Step(r.Id, r.X, r.Y, x, y, now), Is.True); now += round.Course.MovementMilliseconds; }
        return now;
    }
    [Test] public void ReadinessIsIndividualBoundToRaceAndExpires()
    {
        var round = Round(); var a = round.Riders[0];
        Assert.That(round.Begin(1001), Is.False);
        Assert.That(round.Confirm(a.Id, Guid.NewGuid(), 1001), Is.False);
        Assert.That(round.Confirm(Guid.NewGuid(), round.Id, 1001), Is.False);
        Assert.That(round.Confirm(a.Id, round.Id, 1001), Is.True);
        Assert.That(round.Confirm(a.Id, round.Id, 1002), Is.False);
        Assert.That(round.Begin(1003), Is.False);
        Assert.That(round.Confirm(round.Riders[1].Id, round.Id, round.Deadline), Is.False);
        round.Tick(round.Deadline); Assert.That(round.Phase, Is.EqualTo(MountRacePhase.Cancelled));
    }
    [Test] public void CountdownSpeedAndCardinalServerStepsPreventEarlyStartAndTeleport()
    {
        var round = Round(); foreach (var r in round.Riders) round.Confirm(r.Id, round.Id, 1100); round.Begin(1200);
        var a = round.Riders[0]; Assert.That(round.Step(a.Id, 4, 4, 5, 4, round.Deadline - 1), Is.False);
        round.Tick(round.Deadline); var now = round.StartedAt;
        Assert.That(round.Step(a.Id, 4, 4, 5, 5, now), Is.False);
        Assert.That(round.Step(a.Id, 4, 4, 6, 4, now), Is.False);
        Assert.That(round.Step(a.Id, 4, 4, 5, 4, now), Is.True);
        Assert.That(round.Step(a.Id, 5, 4, 6, 4, now + 1), Is.False);
        Assert.That(round.Step(a.Id, 4, 4, 5, 4, now + 180), Is.False);
        Assert.That(round.Step(a.Id, 5, 4, 6, 4, now + 180), Is.True);
        Assert.That(a.Passed, Is.EqualTo(1)); Assert.That(round.Riders[1].Passed, Is.Zero);
    }
    [Test] public void OrderedCheckpointsRequireEveryLapAndRankByServerArrival()
    {
        var round = Round(); var now = Start(round); var a = round.Riders[0]; var b = round.Riders[1];
        Assert.That(round.Step(a.Id, 4, 4, 4, 5, now), Is.True);
        Assert.That(round.Step(a.Id, 4, 5, 4, 6, now + 180), Is.True);
        Assert.That(a.Passed, Is.Zero, "Skipping ahead must not count.");
        round.Step(a.Id, 4, 6, 4, 5, now + 360); round.Step(a.Id, 4, 5, 4, 4, now + 540);
        now = Lap(round, a, now + 720); Assert.That(a.Place, Is.Zero);
        now = Lap(round, a, now); Assert.That(a.Place, Is.EqualTo(1));
        round.Tick(now);
        Assert.That(round.Phase, Is.EqualTo(MountRacePhase.Running), "The first arrival must not stop the other riders.");
        Assert.That(round.CanMove(a.Id, now), Is.False);
        Assert.That(round.CanMove(b.Id, now), Is.True);
        now = Lap(round, b, now); now = Lap(round, b, now); Assert.That(b.Place, Is.EqualTo(2));
        Assert.That(b.ElapsedMilliseconds, Is.GreaterThan(a.ElapsedMilliseconds));
        Assert.That(round.CanMove(a.Id, now), Is.False); round.Tick(now); Assert.That(round.Phase, Is.EqualTo(MountRacePhase.Finished));
    }
    [TestCase(false), TestCase(true)] public void WithdrawalOrTimeoutEndsWithoutFalseWinners(bool timeout)
    {
        var round = Round(); var now = Start(round);
        if (timeout) now = round.Deadline;
        else foreach (var r in round.Riders) round.Withdraw(r.Id);
        round.Tick(now); Assert.That(round.Phase, Is.EqualTo(MountRacePhase.Finished));
        Assert.That(round.Riders.All(r => r.Withdrawn && r.Place == 0), Is.True);
    }
    [Test] public void RulesAreFrozenAndInvalidOrOverlappingCoursesAreRejected()
    {
        var source = Course(); var round = new MountRaceRound(source, [(Guid.NewGuid(), "Practice")], true, 0);
        source.Checkpoints[0].X = 12; source.MovementMilliseconds = 300;
        Assert.That(round.Course.Checkpoints[0].X, Is.EqualTo(6)); Assert.That(round.Course.MovementMilliseconds, Is.EqualTo(180));
        var invalid = Course(); invalid.Checkpoints[0].Radius = 2; Assert.That(invalid.IsValid, Is.False);
        Assert.Throws<InvalidDataException>(() => MountRaceConfiguration.FromJson(new MountRaceConfiguration { Courses = [invalid] }.ToJson()));
        invalid = Course(); invalid.Prizes = [new() { Place = 1, ItemId = Guid.NewGuid() }, new() { Place = 1, ItemId = Guid.NewGuid() }];
        Assert.That(invalid.IsValid, Is.False);
    }
    private RaceJournal Journal() => new(Path.Combine(_folder, "results.json"));
    private static RaceRecord Record(Guid player, Guid item = default) => new() { RaceId = Guid.NewGuid(), CourseId = Guid.NewGuid(), PlayerId = player, ItemId = item,
        Quantity = item == Guid.Empty ? 0 : 12, Name = "Rider", Place = 1, ElapsedMilliseconds = 20000, FinishedUtc = 1000000, Revision = "v1" };
    private static Player Player()
    { var p = new Player { Id = Guid.NewGuid(), Items = new(Options.Instance.Player.MaxInventory, InventorySlot.Create) }; p.Items.FillToCapacity(); GC.SuppressFinalize(p); return p; }
    private ItemDescriptor Prize()
    { var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Race token", ItemType = ItemType.Crafting, Stackable = true, MaxInventoryStack = 10 }; ItemDescriptor.Lookup.Set(item.Id, item); _items.Add(item); return item; }
    [Test] public void MissingOrLegacyCollectionDoesNotLockUnrelatedActivities()
    {
        var p = Player(); p.PetCollection = null!;
        Assert.That(p.IsMountRacing, Is.False);
        Assert.That(p.RaceBlocksMovement, Is.False);
        Assert.That(p.PrepareRaceDisconnect(), Is.False);
        p.PetsJson = "{\"Racing\":null}";
        Assert.That(p.PetCollection.Racing, Is.Not.Null);
        Assert.That(p.IsMountRacing, Is.False);
        p.PetCollection.Racing.Return = new() { MapId = Guid.NewGuid() };
        Assert.That(p.IsMountRacing, Is.True, "A durable return still protects a reconnecting racer.");
    }
    [Test] public void JournalSurvivesReloadDeduplicatesAndAppliesCooldownWithoutErasingRecords()
    {
        var journal = Journal(); var id = Guid.NewGuid(); var item = Guid.NewGuid(); var first = Record(id, item);
        journal.Add(first, 300); journal.Add(first, 300); Assert.That(Journal().Snapshot().Length, Is.EqualTo(1));
        var second = Record(id, item); second.CourseId = first.CourseId; second.FinishedUtc += 2000; journal.Add(second, 300);
        var practice = Record(id, item); practice.Practice = true; journal.Add(practice, 0);
        Assert.That(Journal().Snapshot().Count(r => r.Quantity > 0), Is.EqualTo(1)); Assert.That(Journal().Snapshot().Length, Is.EqualTo(3));
        journal.Write = (_, _) => throw new IOException("Disk full");
        Assert.Throws<IOException>(() => journal.Acknowledge(first.Id, id)); Assert.That(journal.Snapshot()[0].Claimed, Is.False);
        Assert.Throws<IOException>(() => journal.Add(Record(id), 0)); Assert.That(journal.Snapshot().Length, Is.EqualTo(3));
    }
    [TestCase(false), TestCase(true)] public void PrizeSaveFailureRestoresInventoryAndReceipt(bool throws)
    {
        var p = Player(); var item = Prize(); var journal = Journal(); var row = Record(p.Id, item.Id); journal.Add(row, 0);
        if (throws) Assert.Throws<IOException>(() => p.ClaimRacePrize(row.Id, journal, () => throw new IOException("save")));
        else Assert.That(p.ClaimRacePrize(row.Id, journal, () => false), Does.Contain("could not be saved"));
        Assert.That(p.Items.All(s => s.ItemId == Guid.Empty), Is.True); Assert.That(p.PetCollection.Racing.Receipts, Is.Empty); Assert.That(Journal().Snapshot()[0].Claimed, Is.False);
    }
    [Test] public void PrizeHasOwnerRequiresSpaceAndCannotDuplicateAfterJournalAckFailure()
    {
        var p = Player(); var item = Prize(); var journal = Journal(); var row = Record(p.Id, item.Id); journal.Add(row, 0);
        Assert.That(Player().ClaimRacePrize(row.Id, journal, () => true), Does.Contain("no longer available"));
        foreach (var s in p.Items) { s.ItemId = item.Id; s.Quantity = 10; }
        Assert.That(p.ClaimRacePrize(row.Id, journal, () => true), Does.Contain("Make room"));
        foreach (var s in p.Items) s.Set(new Intersect.Server.Database.Item());
        string saved = ""; journal.Write = (_, _) => throw new IOException("ack");
        Assert.Throws<IOException>(() => p.ClaimRacePrize(row.Id, journal, () => { saved = p.PetsJson; return true; }));
        Assert.That(p.Items.Sum(s => s.Quantity), Is.EqualTo(12)); Assert.That(p.Items.Count(s => s.Quantity > 0), Is.EqualTo(2));
        var restored = Player(); restored.Id = p.Id; restored.PetsJson = saved;
        Assert.That(restored.ClaimRacePrize(row.Id, Journal(), () => throw new Exception("must not save twice")), Is.EqualTo("Race prize already claimed."));
        Assert.That(restored.Items.All(s => s.ItemId == Guid.Empty), Is.True); Assert.That(Journal().Snapshot()[0].Claimed, Is.True);
    }
    private sealed class WarpingPlayer : Player
    {
        internal bool Refuse;
        public override void Warp(Guid map, float x, float y, Direction direction, bool adminWarp = false, int zOverride = 0, bool mapSave = false,
            bool fromWarpEvent = false, MapInstanceType? mapInstanceType = null, bool fromLogin = false, bool forceInstanceChange = false)
        { if (Refuse) return; MapId = map; X = (byte)x; Y = (byte)y; Dir = direction; if (mapInstanceType == MapInstanceType.Overworld) { MapInstanceId = Guid.Empty; InstanceType = MapInstanceType.Overworld; } }
    }
    [Test] public void OriginIsSavedBeforeEntryAndSurvivesFailedReturnAndReconnect()
    {
        var p = new WarpingPlayer { Id = Guid.NewGuid(), MapId = Guid.NewGuid(), X = 8, Y = 9, SharedMapInstanceId = Guid.NewGuid() }; GC.SuppressFinalize(p);
        var origin = p.MapId; var partyInstance = p.SharedMapInstanceId;
        var round = new MountRaceRound(Course(), [(p.Id, "Rider"), (Guid.NewGuid(), "Other")], false, 1000);
        foreach (var r in round.Riders) round.Confirm(r.Id, round.Id, 1100);
        Assert.That(p.EnterMountRace(round, () => false), Is.False); Assert.That(p.MapId, Is.EqualTo(origin)); Assert.That(p.IsMountRacing, Is.False);
        string saved = "";
        Assert.That(p.EnterMountRace(round, () => { saved = p.PetsJson; Assert.That(p.MapId, Is.EqualTo(origin)); return true; }), Is.True);
        Assert.That(p.MapInstanceId, Is.EqualTo(round.Id)); Assert.That(p.SharedMapInstanceId, Is.EqualTo(partyInstance)); Assert.That(p.IsActivityCombatProtected, Is.True);
        Assert.That(p.ScriptedMovementMilliseconds, Is.EqualTo(round.Course.MovementMilliseconds)); Assert.That(p.RaceDirectionAllowed(Direction.UpRight), Is.False);
        Assert.That(p.PetCollection.Mounts.Owned, Does.Not.Contain(round.Course.MountId));
        Assert.That(p.ReturnFromMountRace(persist: () => false), Is.False); Assert.That(p.MapId, Is.EqualTo(origin)); Assert.That(p.PetCollection.Racing.Return, Is.Not.Null);
        var reconnect = new WarpingPlayer { Id = p.Id, PetsJson = saved }; GC.SuppressFinalize(reconnect);
        Assert.That(reconnect.ReturnFromMountRace(true, () => true), Is.True); Assert.That((reconnect.MapId, reconnect.X, reconnect.Y), Is.EqualTo((origin, 8, 9)));
        Assert.That(reconnect.IsMountRacing, Is.False); Assert.That(reconnect.ActiveMountId, Is.EqualTo(Guid.Empty));
    }
    [Test] public void DisconnectRemovesPartyWithoutDroppingRecoveryAndFailedWarpStillReturns()
    {
        var p = new WarpingPlayer { Id = Guid.NewGuid(), MapId = Guid.NewGuid(), X = 8, Y = 9, Refuse = true }; GC.SuppressFinalize(p);
        var origin = p.MapId; var other = Player(); p.Party = [p, other]; other.Party = [p, other];
        var round = new MountRaceRound(Course(), [(p.Id, "Rider"), (other.Id, "Other")], false, 1000);
        foreach (var r in round.Riders) round.Confirm(r.Id, round.Id, 1100);
        Assert.That(p.EnterMountRace(round, () => true), Is.False);
        Assert.That(p.PetCollection.Racing.Return!.MapId, Is.EqualTo(origin));
        Assert.That(p.PrepareRaceDisconnect(), Is.True); Assert.That(p.Party, Is.Empty); Assert.That(other.Party, Is.Empty);
        Assert.That(round.Phase, Is.EqualTo(MountRacePhase.Cancelled)); Assert.That(p.PetCollection.Racing.Return, Is.Not.Null);
        p.Refuse = false; Assert.That(p.ReturnFromMountRace(persist: () => true), Is.True); Assert.That(p.MapId, Is.EqualTo(origin));
    }
    [Test] public void UnsafeOrUnreachableTrackCannotBeActivated()
    {
        var course = Course(); var map = new MapController(course.MapId); MapController.Lookup.Set(map.Id, map);
        var registration = new MapController(course.RegistrationMapId); MapController.Lookup.Set(registration.Id, registration);
        var cache = typeof(MountRuntime).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!; var previous = cache.GetValue(null);
        var mount = MountConfiguration.Prototype().Mounts[0]; course.MountId = mount.Id; mount.IntegratedRider = false;
        cache.SetValue(null, new MountConfiguration { Mounts = [mount] });
        try
        {
            Assert.That(MountRaceStore.ValidWorld(course), Is.True);
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) map.Attributes[5, y] = new MapBlockedAttribute();
            Assert.That(MountRaceStore.ValidWorld(course), Is.False);
            map.Attributes[5, 4] = new MapWarpAttribute(); Assert.That(MountRaceStore.ValidWorld(course), Is.False);
            map.Attributes[5, 4] = null; Assert.That(MountRaceStore.ValidWorld(course), Is.True);
        }
        finally { MapController.Lookup.Delete(map); MapController.Lookup.Delete(registration); cache.SetValue(null, previous); }
    }
    [Test] public void ServerMovementGhostsRidersButBlocksWarpsDiagonalsEarlyMovesAndCombat()
    {
        var course = Course(); var map = new MapController(course.MapId); MapController.Lookup.Set(map.Id, map);
        var p = new WarpingPlayer { Id = Guid.NewGuid(), MapId = Guid.NewGuid(), X = 4, Y = 4 }; GC.SuppressFinalize(p);
        var other = Player(); var now = Intersect.Framework.Core.Timing.Global.Milliseconds;
        var round = new MountRaceRound(course, [(p.Id, "Rider"), (other.Id, "Other")], false, now - 6000);
        foreach (var r in round.Riders) round.Confirm(r.Id, round.Id, now - 5500);
        Assert.That(p.EnterMountRace(round, () => true), Is.True);
        var instance = new MapInstance(map, round.Id, p);
        var instances = (System.Collections.Concurrent.ConcurrentDictionary<Guid, MapInstance>)typeof(MapController).GetField("mInstances", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;
        instances[round.Id] = instance;
        typeof(MapController).GetField("mSurroundingMapsWithSelf", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(map, new[] { map });
        other.MapId = map.Id; other.MapInstanceId = round.Id; other.X = 5; other.Y = 4;
        instance.CacheMapBlocks(); instance.AddEntity(p); instance.AddEntity(other);
        try
        {
            Assert.That(p.CanMoveInDirection(Direction.Right, out _, out _), Is.False, "Readiness freezes movement.");
            round.Begin(now - 5000); round.Tick(now);
            Assert.That(p.CanMoveInDirection(Direction.Right, out _, out _), Is.True, "Riders cannot obstruct each other.");
            Assert.That(p.CanMoveInDirection(Direction.UpRight, out _, out _), Is.False);
            map.Attributes[5, 4] = new MapWarpAttribute(); Assert.That(p.CanMoveInDirection(Direction.Right, out _, out _), Is.False);
            map.Attributes[5, 4] = new MapBlockedAttribute(); Assert.That(p.CanMoveInDirection(Direction.Right, out _, out _), Is.False);
            map.Attributes[5, 4] = null; round.Riders[0].NextMoveAt = now + 5000;
            Assert.That(p.CanMoveInDirection(Direction.Right, out _, out _), Is.False, "Server clock throttles client bursts.");
            Assert.That(p.CanAttack(other, null!), Is.False); Assert.That(p.CanCastSpell(null!, other, false, false, out _), Is.False);
            Assert.That(p.ActiveMountId, Is.EqualTo(course.MountId), "A denied attack cannot dismount the rider.");
        }
        finally { instance.RemoveEntity(p); instance.RemoveEntity(other); MapController.Lookup.Delete(map); }
    }
    [Test] public void PacketsAndLocalizationKeepAllIdsAndTokens()
    {
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True); PackedIntersectPacket.AddKnownTypes(registry.Types);
        foreach (var action in Enum.GetValues<MountRaceAction>())
        { var p = new MountRaceRequestPacket { Action = action, CourseId = Guid.NewGuid(), RaceId = Guid.NewGuid(), RecordId = Guid.NewGuid(), RequestId = Guid.NewGuid() };
          var copy = MessagePacker.Instance.Deserialize<MountRaceRequestPacket>(MessagePacker.Instance.Serialize((IntersectPacket)p))!;
          Assert.That((copy.Action, copy.CourseId, copy.RaceId, copy.RecordId, copy.RequestId), Is.EqualTo((p.Action, p.CourseId, p.RaceId, p.RecordId, p.RequestId))); }
        var state = new MountRaceStatePacket { Json = JsonSerializer.Serialize(new MountRaceView { RaceId = Guid.NewGuid() }) };
        Assert.That(MessagePacker.Instance.Deserialize<MountRaceStatePacket>(MessagePacker.Instance.Serialize((IntersectPacket)state))!.Json, Is.EqualTo(state.Json));
        foreach (var entry in QualityOfLifeText.Catalog.Entries) foreach (var language in new[] { "en", "fr", "es" })
            Assert.That(entry.Value.ContainsKey(language) && GameTextTokens.Compatible(entry.Key, entry.Value[language]), Is.True, entry.Key + ":" + language);
        Assert.That(QualityOfLifeText.Get("Royal Races", "fr"), Is.EqualTo("Courses royales"));
        Assert.That(QualityOfLifeText.Get("Royal Races", "invalid"), Is.EqualTo("Royal Races"));
    }
}
