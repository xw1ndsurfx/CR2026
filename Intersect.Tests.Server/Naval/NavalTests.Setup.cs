using Intersect.Framework.Core.Naval;
using Intersect.Localization;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [SetUp] public void SetupNavalOptions() => Options.EnsureCreated();

    [Test]
    public void AutomaticPlacementCoversHullAndKeepsBoardingPointOutsideIt()
    {
        var ship = ShipPlacement.Find(Map, 32, 26, (_, _) => false)!;
        Assert.That(ship.IsValid(), Is.True);
        var hull = ShipFootprint.Tiles(ship.Width, ship.Length, ship.X, ship.Y).ToHashSet();
        Assert.That(hull.All(t => t.X >= 0 && t.X < 32 && t.Y >= 0 && t.Y < 26), Is.True);
        Assert.That(hull.Contains((ship.DockX, ship.DockY)), Is.False);
        var distance = Math.Pow(ship.X - ship.DockX - .5, 2) + Math.Pow(ship.Y - ship.DockY - .5, 2);
        Assert.That(distance, Is.LessThanOrEqualTo(25));
    }

    [Test]
    public void AutomaticPlacementRejectsCrowdedAndTooSmallMaps()
    {
        Assert.That(ShipPlacement.Find(Map, 32, 26, (_, _) => true), Is.Null);
        Assert.That(ShipPlacement.Find(Map, 6, 26, (_, _) => false), Is.Null);
        Assert.That(ShipPlacement.Find(Map, 32, 26, (x, _) => x != 16), Is.Null);
        Assert.That(ShipPlacement.Find(Guid.Empty, 32, 26, (_, _) => false), Is.Null);
    }

    [Test]
    public void AutomaticPlacementAvoidsObstaclesAndReservedShipAndDock()
    {
        var first = ShipPlacement.Find(Map, 32, 26, (_, _) => false)!;
        var reserved = ShipFootprint.Tiles(first.Width, first.Length, first.X, first.Y).ToHashSet();
        reserved.Add((first.DockX, first.DockY)); reserved.Add((0, 0));
        var second = ShipPlacement.Find(Map, 32, 26, (x, y) => reserved.Contains((x, y)))!;
        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(ShipFootprint.Tiles(second.Width, second.Length, second.X, second.Y).Any(reserved.Contains), Is.False);
        Assert.That(reserved.Contains((second.DockX, second.DockY)), Is.False);
    }

    [Test]
    public void AutomaticSetupCreatesEnablesAndIsIdempotentAcrossRestart()
    {
        using var files = new NavalSetupFiles();
        var store = new NavalConfigurationStore(files.Path); var calls = 0;
        var result = store.Prepare(Map, _ => { calls++; return ShipPlacement.Find(Map, 32, 26, (_, _) => false)!; });
        Assert.That(result.Created, Is.True); Assert.That(result.Configuration.Enabled, Is.True);
        var persisted = File.ReadAllText(files.Path);
        var second = new NavalConfigurationStore(files.Path).Prepare(Map, _ => throw new Exception("Duplicate creation"));
        Assert.That(second.Created, Is.False); Assert.That(second.Ship.Id, Is.EqualTo(result.Ship.Id));
        Assert.That(second.Configuration.Ships, Has.Length.EqualTo(1)); Assert.That(calls, Is.EqualTo(1));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(persisted));
    }

    [Test]
    public void AutomaticSetupEnablesExistingShipWithoutChangingItsDefinition()
    {
        using var files = new NavalSetupFiles();
        var ship = new ShipDefinition { MapId = Map, X = 8.5, Y = 9.5, DockX = 12, DockY = 9, MaxSpeed = 2.5 };
        File.WriteAllText(files.Path, JsonConvert.SerializeObject(new NavalConfiguration { Ships = [ship] }));
        var result = new NavalConfigurationStore(files.Path).Prepare(Map, _ => throw new Exception("Existing ship moved"));
        Assert.That(result.Created, Is.False); Assert.That(result.Configuration.Enabled, Is.True);
        Assert.That(JsonConvert.SerializeObject(result.Ship), Is.EqualTo(JsonConvert.SerializeObject(ship)));
        Assert.That(new NavalConfigurationStore(files.Path).Load().Ships[0].MaxSpeed, Is.EqualTo(2.5));
    }

    [TestCase("{invalid")]
    [TestCase("null")]
    [TestCase("{\"Ships\":null}")]
    [TestCase("{\"Ships\":[{\"MapId\":\"11111111-1111-4111-8111-111111111111\"}]}")]
    public void AutomaticSetupNeverOverwritesInvalidConfiguration(string json)
    {
        using var files = new NavalSetupFiles(); File.WriteAllText(files.Path, json);
        Assert.Catch(() => new NavalConfigurationStore(files.Path).Prepare(Map, _ => throw new Exception("Must not create")));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(json));
    }

    [Test]
    public void NoFreePlacementDoesNotCreateOrEnableConfiguration()
    {
        using var files = new NavalSetupFiles(); var store = new NavalConfigurationStore(files.Path);
        Assert.Throws<NoShipPlacementException>(() => store.Prepare(Map, _ => null!));
        Assert.That(File.Exists(files.Path), Is.False);
        File.WriteAllText(files.Path, "{\"Enabled\":false,\"Ships\":[]}");
        var previous = File.ReadAllText(files.Path);
        Assert.Throws<NoShipPlacementException>(() => store.Prepare(Map, _ => null!));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(previous));
    }

    [Test]
    public void SetupPreservesOtherShipsAndUnknownRootSettings()
    {
        using var files = new NavalSetupFiles();
        var first = new ShipDefinition { MapId = Guid.NewGuid(), X = 9.5, Y = 9.5, DockX = 13, DockY = 9 };
        var configuration = new NavalConfiguration { Ships = [first], AdditionalData = new Dictionary<string, Newtonsoft.Json.Linq.JToken> { ["FutureOption"] = "preserve" } };
        File.WriteAllText(files.Path, JsonConvert.SerializeObject(configuration));
        var originalJson = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(files.Path));
        originalJson["Ships"]![0]!["FutureGraphics"] = "keep-ship-metadata";
        File.WriteAllText(files.Path, originalJson.ToString());
        var result = new NavalConfigurationStore(files.Path).Prepare(Map, _ => ShipPlacement.Find(Map, 32, 26, (_, _) => false)!);
        Assert.That(result.Configuration.Ships, Has.Length.EqualTo(2));
        var saved = new NavalConfigurationStore(files.Path).Load();
        Assert.That(saved.Ships[0].Id, Is.EqualTo(first.Id)); Assert.That(saved.Ships[0].X, Is.EqualTo(9.5));
        Assert.That(saved.AdditionalData["FutureOption"].ToString(), Is.EqualTo("preserve"));
        Assert.That(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(files.Path))["Ships"]![0]!["FutureGraphics"]!.ToString(), Is.EqualTo("keep-ship-metadata"));
    }

    [Test]
    public void SetupRejectsLimitAndDuplicateIdsWithoutWriting()
    {
        using var files = new NavalSetupFiles();
        var ships = Enumerable.Range(0, 64).Select(_ => new ShipDefinition { MapId = Guid.NewGuid() }).ToArray();
        File.WriteAllText(files.Path, JsonConvert.SerializeObject(new NavalConfiguration { Enabled = true, Ships = ships }));
        var previous = File.ReadAllText(files.Path); var store = new NavalConfigurationStore(files.Path);
        Assert.Throws<ShipLimitException>(() => store.Prepare(Map, _ => throw new Exception()));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(previous));
        Assert.That(store.Prepare(ships[0].MapId, _ => throw new Exception()).Created, Is.False);
        ships[1].Id = ships[0].Id;
        File.WriteAllText(files.Path, JsonConvert.SerializeObject(new NavalConfiguration { Ships = ships }));
        Assert.Throws<InvalidDataException>(() => store.Load());
    }

    [Test]
    public void ConcurrentSetupClicksCreateOnlyOneShip()
    {
        using var files = new NavalSetupFiles(); var store = new NavalConfigurationStore(files.Path);
        var ids = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        Parallel.For(0, 8, _ => ids.Add(store.Prepare(Map, _ => ShipPlacement.Find(Map, 32, 26, (_, _) => false)!).Ship.Id));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(1)); Assert.That(store.Load().Ships, Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(files.Directory), Has.Length.EqualTo(1));
    }

    [Test]
    public void SetupRejectsConfigurationChangedDuringPlacement()
    {
        using var files = new NavalSetupFiles(); var store = new NavalConfigurationStore(files.Path);
        const string external = "{\"Enabled\":false,\"Ships\":[],\"ExternalEdit\":true}";
        Assert.Throws<IOException>(() => store.Prepare(Map, _ =>
        {
            File.WriteAllText(files.Path, external);
            return ShipPlacement.Find(Map, 32, 26, (_, _) => false)!;
        }));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(external));
    }

    [Test]
    public void SetupRequestIsEditorOnlyAndNewPacketsRoundTrip()
    {
        EditorPacket request = new PrepareNavalMapPacket { MapId = Map };
        var copy = MessagePackSerializer.Deserialize<EditorPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(copy, Is.TypeOf<PrepareNavalMapPacket>());
        Assert.That(((PrepareNavalMapPacket)copy).MapId, Is.EqualTo(Map));
        var result = new NavalSetupResultPacket { Outcome = NavalSetupOutcome.Created, X = 10.5, DockX = 14, ShipId = Guid.NewGuid() };
        var restored = MessagePackSerializer.Deserialize<NavalSetupResultPacket>(MessagePackSerializer.Serialize(result));
        Assert.That(restored.Outcome, Is.EqualTo(result.Outcome)); Assert.That(restored.ShipId, Is.EqualTo(result.ShipId));
        Assert.That(restored.X, Is.EqualTo(10.5));
        Assert.That(NavalSystem.PrepareMap(Guid.NewGuid()).Outcome, Is.EqualTo(NavalSetupOutcome.NotOcean));
    }

    [Test]
    public void SetupCatalogCoversAllMessagesAndPreservesFormattingTokens()
    {
        foreach (var field in typeof(NavalSetupText).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            var source = (string)field.GetRawConstantValue()!;
            foreach (var language in GameTextCatalog.Languages)
            {
                Assert.That(NavalSetupText.Catalog.Entries[source].ContainsKey(language), Is.True);
                Assert.That(GameTextTokens.Compatible(source, NavalSetupText.Get(source, language)), Is.True);
            }
            Assert.That(NavalSetupText.Get(source, "unsupported"), Is.EqualTo(source));
        }
    }

    private sealed class NavalSetupFiles : IDisposable
    {
        internal string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "naval-setup-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Directory, "naval.json");
        internal NavalSetupFiles() => System.IO.Directory.CreateDirectory(Directory);
        public void Dispose()
        {
            foreach (var file in System.IO.Directory.GetFiles(Directory)) File.Delete(file);
            System.IO.Directory.Delete(Directory);
        }
    }
}
