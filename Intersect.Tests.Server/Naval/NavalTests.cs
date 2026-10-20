using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Localization;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using MessagePack;
using NUnit.Framework;
using System.Runtime.CompilerServices;
using Intersect.Server.Entities;

namespace Intersect.Tests.Server.Naval;

[TestFixture, NonParallelizable]
public sealed partial class NavalTests
{
    private static readonly Guid Map = Guid.NewGuid();
    private static ShipDefinition Definition() => new() { MapId = Map };
    private static (bool, Guid, double, double) Clear(Guid map, double x, double y, double heading) => (true, map, x, y);
    private static void Simulate(ShipState state, ShipDefinition definition, ShipInput input, int steps = 100, double dt = .05)
    { for (var i = 0; i < steps; i++) ShipMovement.Step(state, definition, input, dt, Clear); }

    [Test]
    public void AccelerationIsGradualAndForwardSpeedIsCapped()
    {
        var s = new ShipState { MapId = Map, X = 10, Y = 10 }; var d = Definition();
        Simulate(s, d, new(1, 0, false), 1);
        Assert.That(s.Speed, Is.LessThan(.1));
        Simulate(s, d, new(1, 0, false), 1000);
        Assert.That(s.Speed, Is.EqualTo(d.MaxSpeed).Within(.001));
        Assert.That(s.Y, Is.LessThan(10)); Assert.That(s.X, Is.EqualTo(10));
    }

    [Test]
    public void ReleaseCoastsAndAnchorStopsWithoutTeleporting()
    {
        var s = new ShipState { MapId = Map, Speed = 2 }; var d = Definition();
        Simulate(s, d, new(0, 0, false), 1);
        Assert.That(s.Speed, Is.InRange(1.9, 2)); Assert.That(s.Y, Is.LessThan(0));
        Simulate(s, d, new(0, 0, true));
        Assert.That(s.Speed, Is.Zero); Assert.That(s.Throttle, Is.Zero);
    }

    [Test]
    public void BrakeCrossesZeroThenAllowsOnlySlowReverse()
    {
        var s = new ShipState { Speed = 2, Throttle = 1 }; var d = Definition();
        Simulate(s, d, new(-1, 0, false), 1000);
        Assert.That(s.Speed, Is.EqualTo(-d.ReverseSpeed).Within(.001));
    }

    [Test]
    public void HeadingIsContinuousAndRotationLimited()
    {
        var s = new ShipState { Heading = 359 }; var d = Definition();
        Simulate(s, d, new(0, 1, false), 1, .1);
        Assert.That(s.Heading, Is.EqualTo(3.5).Within(.001));
        Assert.That(ShipMovement.DirectionRow(359), Is.Zero);
        Assert.That(ShipMovement.DirectionRow(22.49), Is.Zero);
        Assert.That(ShipMovement.DirectionRow(22.5), Is.EqualTo(1));
        Assert.That(ShipMovement.DirectionRow(337.5), Is.Zero);
    }

    [Test]
    public void PhysicsIsStableAcrossTickRates()
    {
        var a = new ShipState(); var b = new ShipState(); var d = Definition();
        Simulate(a, d, new(1, 1, false), 200, .025);
        Simulate(b, d, new(1, 1, false), 100, .05);
        Assert.That(a.X, Is.EqualTo(b.X).Within(.08));
        Assert.That(a.Y, Is.EqualTo(b.Y).Within(.08));
        Assert.That(a.Heading, Is.EqualTo(b.Heading).Within(.001));
    }

    [Test]
    public void PausesCannotCauseLongJumpsAndInvalidDeltaIsIgnored()
    {
        var s = new ShipState { Speed = 3 };
        ShipMovement.Step(s, Definition(), new(1, 0, false), 20, Clear);
        Assert.That(Math.Abs(s.Y), Is.LessThanOrEqualTo(.301));
        var previous = s.Y;
        ShipMovement.Step(s, Definition(), default, double.NaN, Clear);
        Assert.That(s.Y, Is.EqualTo(previous));
    }

    [Test]
    public void WholeHullStopsBeforeATileWallEvenAtMaximumSpeed()
    {
        var s = new ShipState { MapId = Map, X = 10, Y = 10, Speed = 10, Heading = 90 };
        var d = Definition(); d.MaxSpeed = 10;
        for (var i = 0; i < 30; i++)
            ShipMovement.Step(s, d, new(1, 0, false), .1, (map, x, y, h) =>
                (!ShipFootprint.Tiles(d.Width, d.Length, x, y).Any(t => t.X == 14), map, x, y));
        Assert.That(s.X, Is.LessThan(14 - Math.Sqrt(20) / 2));
        Assert.That(s.Speed, Is.Zero);
        Assert.That(ShipFootprint.Tiles(2, 4, 10, 10).Contains((12, 10)), Is.True);
    }

    [Test]
    public void InputRequiresOwnershipAndRejectsMalformedOrFloodedInputs()
    {
        var actor = Guid.NewGuid(); var s = new ShipState { CaptainId = actor }; var authority = new ShipInputAuthority();
        var packet = new ShipInputPacket { ShipId = s.Id, Thrust = 1, Turn = -1 };
        Assert.That(authority.Accept(s, Guid.NewGuid(), true, packet, 1000), Is.False);
        Assert.That(authority.Accept(s, actor, false, packet, 1000), Is.False);
        packet.ShipId = Guid.NewGuid();
        Assert.That(authority.Accept(s, actor, true, packet, 1000), Is.False);
        packet.ShipId = s.Id; packet.Thrust = 127;
        Assert.That(authority.Accept(s, actor, true, packet, 1000), Is.False);
        packet.Thrust = 1;
        Assert.That(authority.Accept(s, actor, true, packet, 1000), Is.True);
        packet.Turn = 1;
        Assert.That(authority.Accept(s, actor, true, packet, 1010), Is.False);
        Assert.That(authority.Input.Turn, Is.EqualTo(-1));
        Assert.That(authority.ForTick(1501, false).Anchor, Is.True);
        Assert.That(authority.ForTick(1100, true).Anchor, Is.True);
        authority.Reset(); Assert.That(authority.Input.Anchor, Is.True);
    }

    [Test]
    public void IndependentShipsKeepSeparateInputsAndOwnership()
    {
        var one = new ShipState { CaptainId = Guid.NewGuid() }; var two = new ShipState { CaptainId = Guid.NewGuid() };
        var first = new ShipInputAuthority(); var second = new ShipInputAuthority();
        var packet = new ShipInputPacket { ShipId = one.Id, Thrust = 1 };
        Assert.That(first.Accept(one, one.CaptainId, true, packet, 1000), Is.True);
        Assert.That(second.Accept(two, one.CaptainId, true, packet, 1000), Is.False);
        Assert.That(second.Input.Anchor, Is.True);
    }

    [Test]
    public void OrdinaryPlayerLifecycleDoesNotRequireNavalInitialization()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        Assert.That(player.CaptainedShipId, Is.EqualTo(Guid.Empty));
        Assert.DoesNotThrow(() => NavalSystem.Release(player));
    }

    [Test]
    public void OceanFoldersIncludeDescendantsButDoNotMatchMapNames()
    {
        var saved = MapList.List;
        try
        {
            var included = Guid.NewGuid(); var nested = Guid.NewGuid(); var excluded = Guid.NewGuid();
            MapList.List = new() { Items = [new MapListMap { Name = "Ocean", MapId = excluded },
                new MapListFolder { Name = "World", Children = new() { Items = [
                    new MapListFolder { Name = "Ocean", Children = new() { Items = [
                        new MapListMap { MapId = included },
                        new MapListFolder { Name = "Islands", Children = new() { Items = [new MapListMap { MapId = nested }] } }
                    ] } }
                ] } }
            ] };
            Assert.That(OceanMapResolver.Maps(), Is.EquivalentTo(new[] { included, nested }));
        }
        finally { MapList.List = saved; }
    }

    [Test]
    public void TransitionsPreserveFractionalOvershootAndRejectLandAndBrokenLinks()
    {
        Options.EnsureCreated();
        var a = new MapController(Guid.NewGuid()); var b = new MapController(Guid.NewGuid());
        MapController.Lookup.Set(a.Id, a); MapController.Lookup.Set(b.Id, b);
        try
        {
            a.Right = b.Id; b.Left = a.Id;
            var ocean = new HashSet<Guid> { a.Id, b.Id }; var id = a.Id;
            double x = Options.Instance.Map.MapWidth + .25, y = 5.75;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.True);
            Assert.That(id, Is.EqualTo(b.Id)); Assert.That(x, Is.EqualTo(.25)); Assert.That(y, Is.EqualTo(5.75));
            id = b.Id; x = -.4;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.True);
            Assert.That(x, Is.EqualTo(Options.Instance.Map.MapWidth - .4).Within(.001));
            ocean.Remove(b.Id); id = a.Id; x = Options.Instance.Map.MapWidth + .1;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.False);
            ocean.Add(b.Id); b.Left = Guid.Empty; id = a.Id; x = Options.Instance.Map.MapWidth + .1;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.False);
        }
        finally { MapController.Lookup.Delete(a); MapController.Lookup.Delete(b); }
    }

    [Test]
    public void ShipCollisionConservativelyRespectsMapAttributes()
    {
        Assert.That(NavalSystem.IsBlockingAttribute(null!), Is.False);
        Assert.That(NavalSystem.IsBlockingAttribute(new MapSoundAttribute()), Is.False);
        Assert.That(NavalSystem.IsBlockingAttribute(new MapAnimationAttribute { IsBlock = false }), Is.False);
        foreach (var a in new MapAttribute[] { new MapBlockedAttribute(), new MapWarpAttribute(), new MapResourceAttribute(),
            new MapSlideAttribute(), new MapGrappleStoneAttribute(), new MapZDimensionAttribute(), new MapAnimationAttribute { IsBlock = true } })
            Assert.That(NavalSystem.IsBlockingAttribute(a), Is.True);
    }

    [Test]
    public void PacketsRoundTripWithoutExposingAuthoritativeCoordinatesInInput()
    {
        var input = new ShipInputPacket { ShipId = Guid.NewGuid(), Thrust = -1, Turn = 1, Anchor = true };
        var restored = MessagePackSerializer.Deserialize<ShipInputPacket>(MessagePackSerializer.Serialize(input));
        Assert.That(restored.ShipId, Is.EqualTo(input.ShipId)); Assert.That(restored.Thrust, Is.EqualTo(-1));
        var snapshot = new NavalSnapshotPacket { Ships = [new() { Id = input.ShipId, MapId = Map, X = 4.125, Heading = 359.5, Frames = 4 }] };
        var result = MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(snapshot));
        Assert.That(result.Ships[0].X, Is.EqualTo(4.125)); Assert.That(result.Ships[0].Heading, Is.EqualTo(359.5));
    }

    [Test]
    public void DefinitionRejectsUnsafePhysicsAndAssetPaths()
    {
        var d = Definition(); Assert.That(d.IsValid(), Is.True);
        d.MaxSpeed = double.PositiveInfinity; Assert.That(d.IsValid(), Is.False);
        d.MaxSpeed = 3; d.Graphics = "../other.png"; Assert.That(d.IsValid(), Is.False);
        d.Graphics = "royal-ship.png"; d.Frames = 0; Assert.That(d.IsValid(), Is.False);
    }

    [Test]
    public void NavalMessagesHaveThreeCompatibleTranslationsAndEnglishFallback()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Intersect.Client.Core", "Localization", "Locales", "ui.json")))
            root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var catalog = GameTextCatalog.FromJson(File.ReadAllText(Path.Combine(root!.FullName, "Intersect.Client.Core", "Localization", "Locales", "ui.json")));
        var messages = new[] { "Naval navigation is unavailable.", "Stop near the helm dock to disembark safely.",
            "You have left the helm.", "No available ship near this helm dock.",
            "Anchor with Space and stop the ship before disembarking.",
            "Return to the helm dock at ({0}, {1}) on its original map to disembark.",
            "No free exit near this dock. Move the ship slightly and try again.",
            "Helm acquired. W/S: thrust/brake, A/D: turn, Space: anchor. /naval: leave at dock." };
        foreach (var source in messages)
        {
            foreach (var language in GameTextCatalog.Languages)
            {
                Assert.That(catalog.Entries[source].ContainsKey(language), Is.True);
                Assert.That(GameTextTokens.Compatible(source, catalog.Get(source, language)), Is.True);
                Assert.That(catalog.Get(source, language), Is.Not.Empty);
            }
            Assert.That(catalog.Get(source, "unsupported"), Is.EqualTo(source));
        }
        Assert.That(catalog.GetExpanded("Return to the helm dock at (15, 9) on its original map to disembark.","fr"),
            Is.EqualTo("Revenez au quai de la barre en (15, 9), sur sa carte d’origine, pour débarquer."));
    }

    [Test]
    public void VerticalTransitionsKeepTheirFractionalPositionAndCannotLeaveOcean()
    {
        Options.EnsureCreated();
        var a = new MapController(Guid.NewGuid()); var b = new MapController(Guid.NewGuid());
        MapController.Lookup.Set(a.Id, a); MapController.Lookup.Set(b.Id, b);
        try
        {
            a.Down = b.Id; b.Up = a.Id;
            var ocean = new HashSet<Guid> { a.Id, b.Id }; var id = a.Id;
            double x = 6.25, y = Options.Instance.Map.MapHeight + .75;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.True);
            Assert.That(id, Is.EqualTo(b.Id)); Assert.That(y, Is.EqualTo(.75)); Assert.That(x, Is.EqualTo(6.25));
            id = b.Id; y = -.4;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.True);
            Assert.That(id, Is.EqualTo(a.Id)); Assert.That(y, Is.EqualTo(Options.Instance.Map.MapHeight - .4).Within(.001));
            id = a.Id; y = -.1;
            Assert.That(OceanMapResolver.Resolve(ocean, ref id, ref x, ref y), Is.False);
        }
        finally { MapController.Lookup.Delete(a); MapController.Lookup.Delete(b); }
    }
}
