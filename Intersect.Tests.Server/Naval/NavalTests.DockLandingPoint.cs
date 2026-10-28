using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    public sealed class LegacyEmptyDock { }

    [Test]
    public void DockLandingFieldsSurviveCloneAndNativeSerializationAndOldEmptyDocksRemainAutomatic()
    {
        var dock = new MapDockAttribute { HasLandingPoint = true, LandingX = 20, LandingY = 6 };
        var clone = (MapDockAttribute)dock.Clone();
        Assert.That(clone.Data(), Is.EqualTo(dock.Data()));
        clone.LandingX = 12; Assert.That(dock.LandingX, Is.EqualTo(20));
        var attrs = new MapAttribute[4, 4]; attrs[2, 2] = dock;
        var native = new Intersect.Network.Ceras(false);
        var restored = (MapDockAttribute)native.Deserialize<MapAttribute[,]>(native.Serialize(attrs))![2, 2];
        Assert.That(restored.Data(), Is.EqualTo(dock.Data()));
        Assert.That(JsonConvert.DeserializeObject<MapDockAttribute>("{}")!.HasLandingPoint, Is.False);
        // Standard version-tolerant Ceras record with the old, empty Q schema.
        var old = new Intersect.Network.Ceras(new Dictionary<string, Type> { ["dock"] = typeof(LegacyEmptyDock) });
        var upgraded = new Intersect.Network.Ceras(new Dictionary<string, Type> { ["dock"] = typeof(MapDockAttribute) });
        Assert.That(upgraded.Deserialize<MapDockAttribute>(old.Serialize(new LegacyEmptyDock()))!.HasLandingPoint, Is.False);
    }

    [Test]
    public void ExplicitArrivalUsesTheConfiguredWalkablePierTileAndKeepsItsQuayIdentity()
    {
        using var world = new EnemyTestWorld(); var vessel = world.Add(8.5, 10.5, false);
        world.Map.Attributes[8, 7] = new MapDockAttribute { HasLandingPoint = true, LandingX = 20, LandingY = 6 };
        var player = new Player { MapId = world.Map.Id }; GC.SuppressFinalize(player);
        var landing = (NavalSystem.DockLanding)world.Invoke("FindDockLanding", player, vessel)!;
        Assert.That((landing.X, landing.Y), Is.EqualTo((20, 6)), "The authored pier exit can be farther than the interaction radius");
        Assert.That((landing.DockX, landing.DockY), Is.EqualTo((8, 7)));
        Assert.That(landing.MapId, Is.EqualTo(world.Map.Id));
        world.Map.Attributes[20, 6] = new MapBlockedAttribute();
        Assert.That(world.Invoke("FindDockLanding", player, vessel), Is.Null, "Never silently substitute another landing for a blocked explicit point");
        world.Map.Attributes[20, 6] = null;
        var dock = (MapDockAttribute)world.Map.Attributes[8, 7]; dock.LandingX = 8; dock.LandingY = 10;
        Assert.That(world.Invoke("FindDockLanding", player, vessel), Is.Null, "Never spawn the character inside the hull");
    }

    [TestCase(-1, 5)] [TestCase(32, 5)] [TestCase(5, -1)] [TestCase(5, 26)]
    public void InvalidExplicitArrivalCannotFallBackIntoWater(int x, int y)
    {
        var attrs = new MapAttribute[32, 26]; attrs[8, 7] = new MapDockAttribute { HasLandingPoint = true, LandingX = x, LandingY = y };
        Assert.That(ShipDock.FindLanding(attrs, 8.5, 10.5, 5, (_, _) => true), Is.Null);
    }

    [Test]
    public void NearestQuayOwnsItsArrivalAndQItselfCannotBeTheArrival()
    {
        var attrs = new MapAttribute[32, 26];
        var first = new MapDockAttribute { HasLandingPoint = true, LandingX = 12, LandingY = 4 };
        attrs[8, 7] = first; attrs[12, 7] = new MapDockAttribute { HasLandingPoint = true, LandingX = 25, LandingY = 4 };
        Assert.That(ShipDock.FindLanding(attrs, 8.5, 10.5, 5, (_, _) => true), Is.EqualTo((12, 4)));
        first.LandingX = 8; first.LandingY = 7;
        Assert.That(ShipDock.FindLanding(attrs, 8.5, 10.5, 5, (_, _) => true), Is.Null);
        first.HasLandingPoint = false;
        Assert.That(ShipDock.FindLanding(attrs, 8.5, 10.5, 5, (_, _) => true), Is.Not.Null, "Legacy automatic docking remains available");
    }
}
