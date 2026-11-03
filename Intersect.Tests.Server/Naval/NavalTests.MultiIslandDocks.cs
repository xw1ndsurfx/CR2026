using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void PaintedQuayUsesTheCurrentIslandInsteadOfTheLaunchIslandForCaptainAndCrew()
    {
        using var world = new EnemyTestWorld();
        var ship = world.Add(8.5, 10.5, false);
        var launch = world.Field<ShipDefinition>(ship, "Definition");
        launch.MapId = Guid.NewGuid(); launch.DockX = 2; launch.DockY = 2; // Original Polonya berth.
        world.Map.Attributes[8, 7] = new MapDockAttribute();
        var captain = new Player { MapId = world.Map.Id, CaptainedShipId = world.State(ship).Id };
        var passenger = new Player { MapId = world.Map.Id, BoardedShipId = world.State(ship).Id };
        GC.SuppressFinalize(captain); GC.SuppressFinalize(passenger);
        foreach (var player in new[] { captain, passenger })
        {
            var landing = (NavalSystem.DockLanding?)world.Invoke("FindDockLanding", player, ship);
            Assert.That(landing, Is.Not.Null);
            Assert.That(landing!.MapId, Is.EqualTo(world.Map.Id).And.Not.EqualTo(launch.MapId));
            Assert.That((landing.DockX, landing.DockY), Is.EqualTo((8, 7)));
            Assert.That(Math.Abs(landing.X - 8) + Math.Abs(landing.Y - 7), Is.EqualTo(1));
            Assert.That(ShipFootprint.Tiles(launch.Width, launch.Length, 8.5, 10.5), Does.Not.Contain((landing.X, landing.Y)));
        }
        world.State(ship).Speed = .11;
        Assert.That(world.Invoke("FindDockLanding", captain, ship), Is.Null);
        world.State(ship).Speed = 0;
        foreach (var (x, y) in new[] { (8, 6), (7, 7), (9, 7), (8, 8) }) world.Map.Attributes[x, y] = new MapBlockedAttribute();
        Assert.That(world.Invoke("FindDockLanding", captain, ship), Is.Null, "An occupied quay cannot send the player to the old port instead");
        Assert.That(captain.CaptainedShipId, Is.EqualTo(world.State(ship).Id));
    }

    [Test]
    public void MooringOnSeveralIslandsPersistsTheNewBerthAndPreservesCargoAndIdentity()
    {
        var captain = new Player(); var passenger = new Player();
        GC.SuppressFinalize(captain); GC.SuppressFinalize(passenger);
        var original = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        var ship = new OwnedShip { ModelId = "personal-model", Name = "Voyageur", AtSea = true, Health = 72,
            ReturnMapId = original, ReturnX = 2, ReturnY = 3, ReturnDockX = 2, ReturnDockY = 4,
            Cargo = [new() { ItemId = Guid.NewGuid(), Quantity = 4 }] };
        captain.NavalFleet.Add(ship);
        passenger.NavalCrewPassage = new() { CaptainId = captain.Id, Ship = JsonConvert.DeserializeObject<OwnedShip>(JsonConvert.SerializeObject(ship))! };
        foreach (var arrival in new[] { new NavalSystem.DockLanding(second, 9, 8, 9, 9), new(third, 15, 12, 16, 12), new(original, 4, 5, 4, 6) })
        {
            foreach (var hold in new[] { captain.NavalFleet[0], passenger.NavalCrewPassage!.Ship })
            {
                Assert.That(NavalSystem.CommitDockingReturn(hold, arrival, () => true), Is.True);
                Assert.That(hold.AtSea, Is.True, "Keep a crash-recovery marker until release and warp complete");
                Assert.That(ShipReturnPoint.Exact(hold, 32, 26, (_, _) => true), Is.EqualTo((arrival.X, arrival.Y)));
            }
            var savedCaptain = captain.NavalFleetJson; var savedPassenger = passenger.NavalFleetJson;
            captain.NavalFleetJson = savedCaptain; passenger.NavalFleetJson = savedPassenger;
            foreach (var hold in new[] { captain.NavalFleet[0], passenger.NavalCrewPassage!.Ship })
            {
                Assert.That(hold.ReturnMapId, Is.EqualTo(arrival.MapId));
                Assert.That((hold.ReturnDockX, hold.ReturnDockY), Is.EqualTo((arrival.DockX, arrival.DockY)));
                Assert.That(hold.Id, Is.EqualTo(ship.Id)); Assert.That(hold.ModelId, Is.EqualTo(ship.ModelId));
                Assert.That(hold.Name, Is.EqualTo("Voyageur")); Assert.That(hold.Health, Is.EqualTo(72));
                Assert.That(hold.Cargo.Single().Quantity, Is.EqualTo(4));
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrewStaysAboardWhenTheNewDockCannotBePersisted(bool captainAlreadyMoored)
    {
        using var world = new EnemyTestWorld();
        var vessel = world.Add(8.5, 10.5, false);
        world.Map.Attributes[8, 7] = new MapDockAttribute();
        var passenger = new Player { MapId = world.Map.Id, BoardedShipId = world.State(vessel).Id };
        GC.SuppressFinalize(passenger);
        AddCrewPresence(world, vessel, passenger.Id, Timing.Global.Milliseconds);
        passenger.NavalCrewPassage = new() { Ship = new() { Id = world.State(vessel).Id, AtSea = true,
            ReturnMapId = Guid.NewGuid(), ReturnX = 2, ReturnY = 3, ReturnDockX = 2, ReturnDockY = 4 } };
        var before = passenger.NavalFleetJson;
        if (captainAlreadyMoored)
        {
            world.State(vessel).CaptainId = Guid.Empty;
            world.Set(vessel, "Mooring", new NavalSystem.DockLanding(world.Map.Id, 8, 6, 8, 7));
        }
        // No persistent User in this isolated fixture: saving is refused.
        world.Invoke("ReleaseCrew", passenger, true, !captainAlreadyMoored);
        Assert.That(passenger.BoardedShipId, Is.EqualTo(world.State(vessel).Id));
        Assert.That(passenger.NavalFleetJson, Is.EqualTo(before));
        Assert.That(world.Ships.Contains(world.State(vessel).Id), Is.True);
        Assert.That(((System.Collections.IDictionary)world.Field<object>(vessel, "Crew")).Contains(passenger.Id), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReconnectingCrewFollowsACompletedMooringButRetainsRescueWhileCaptainIsAtSea(bool stillAtSea)
    {
        var origin = Guid.NewGuid(); var island = Guid.NewGuid();
        var source = new OwnedShip { AtSea = stillAtSea, ReturnMapId = island, ReturnX = 8, ReturnY = 9, ReturnDockX = 8, ReturnDockY = 10 };
        var passage = new ShipCrewPassage { Ship = new() { Id = source.Id, AtSea = true, ReturnMapId = origin, ReturnX = 2, ReturnY = 3, Cargo = [new() { Quantity = 4 }] } };
        NavalSystem.ReconcileCrewCargo(passage, JsonConvert.SerializeObject(new NavalPlayerData { Ships = [source] }));
        Assert.That(passage.Ship.ReturnMapId, Is.EqualTo(stillAtSea ? origin : island));
        Assert.That((passage.Ship.ReturnX, passage.Ship.ReturnY), Is.EqualTo(stillAtSea ? (2, 3) : (8, 9)));
        Assert.That(passage.Ship.Cargo.Single().Quantity, Is.EqualTo(4));
        Assert.That(passage.Ship.AtSea, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedMooringSaveRestoresThePreviousRescuePoint(bool throws)
    {
        var ship = new OwnedShip { AtSea = true, ReturnMapId = Guid.NewGuid(), ReturnX = 2, ReturnY = 3, ReturnDockX = 2, ReturnDockY = 4 };
        var before = JsonConvert.SerializeObject(ship);
        bool Save() { if (throws) throw new IOException("Test persistence failure"); return false; }
        if (throws) Assert.Throws<IOException>(() => NavalSystem.CommitDockingReturn(ship, new(Guid.NewGuid(), 8, 9, 8, 10), Save));
        else Assert.That(NavalSystem.CommitDockingReturn(ship, new(Guid.NewGuid(), 8, 9, 8, 10), Save), Is.False);
        Assert.That(JsonConvert.SerializeObject(ship), Is.EqualTo(before));
    }
}
