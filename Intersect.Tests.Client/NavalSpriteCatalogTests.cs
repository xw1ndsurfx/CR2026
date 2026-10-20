using Intersect.Client.Naval;
using Intersect.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class NavalSpriteCatalogTests
{
    [Test]
    public void SnapshotRefreshDoesNotRestartSinkingAndEachVesselHasItsOwnClock()
    {
        var clock = new ShipAnimationClock();
        clock.Observe("normal.png",1000);
        clock.Observe("normal.png",1100);
        Assert.That(clock.Elapsed(1200),Is.EqualTo(200));
        clock.Observe("sink.png",2000);
        Assert.That(clock.Elapsed(2000),Is.Zero);
        clock.Observe("SINK.png",2100);
        Assert.That(clock.Elapsed(2500),Is.EqualTo(500));
        var other = new ShipAnimationClock(); other.Observe("sink.png",2400);
        Assert.That(other.Elapsed(2500),Is.EqualTo(100));
        clock.Observe("normal.png",3000);
        Assert.That(clock.Elapsed(3000),Is.Zero);
        Assert.That(clock.Elapsed(2900),Is.Zero);
    }

    [Test]
    public void MissingOrInvalidOceanSidecarsFallBackAndCannotEscapeTheResourceDirectory()
    {
        var previous = ClientConfiguration.ResourcesDirectory;
        var directory = Path.Combine(Path.GetTempPath(),"naval-sprite-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory,"ocean"));
        try
        {
            ClientConfiguration.ResourcesDirectory = directory;
            NavalSpriteCatalog.Clear();
            File.WriteAllText(Path.Combine(directory,"ocean","ships.sinking.json"),
                "{\"Ship_CorpsRoyaux_002.png\":\"Ship_CorpsRoyaux_002_Sink.png\",\"bad.png\":\"../escape.png\"}");
            Assert.That(NavalSpriteCatalog.GetSinkingGraphics("royal-ship.png"),Is.EqualTo("Ship_CorpsRoyaux_002_Sink.png"));
            Assert.That(NavalSpriteCatalog.GetSinkingGraphics("bad.png"),Is.Null);
            Assert.That(NavalSpriteCatalog.GetSinkingGraphics("unknown.png"),Is.Null);
            Assert.That(NavalSpriteCatalog.LegacyAlias("royal-ship.png"),Is.EqualTo("Ship_CorpsRoyaux_002.png"));
            Assert.That(NavalSpriteCatalog.LegacyAlias("my-custom.png"),Is.EqualTo("my-custom.png"));
            Assert.That(NavalSpriteCatalog.Get("missing.png",100,100,4),Is.Null);
            File.WriteAllText(Path.Combine(directory,"ocean","invalid.png.naval.json"),"{broken");
            Assert.That(NavalSpriteCatalog.Get("invalid.png",100,100,4),Is.Null);
            Assert.That(NavalSpriteCatalog.Get("../escape.png",100,100,4),Is.Null);
            Assert.That(NavalSpriteCatalog.Get("..\\escape.png",100,100,4),Is.Null);
            var atlas = new Intersect.Framework.Core.Naval.ShipSpriteAtlas
            {
                ImageWidth=100,ImageHeight=100,
                Directions=Enumerable.Range(0,8).Select(_=>Enumerable.Range(0,4).Select(_=>
                    new Intersect.Framework.Core.Naval.ShipSpriteFrame { Width=20,Height=20,AnchorX=10,AnchorY=15 }).ToArray()).ToArray()
            };
            var path=Path.Combine(directory,"ocean","valid.png.naval.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(atlas));
            Assert.That(NavalSpriteCatalog.Get("valid.png",100,100,4),Is.Not.Null);
            Assert.That(NavalSpriteCatalog.Get("valid.png",101,100,4),Is.Null);
            Assert.That(NavalSpriteCatalog.Get("valid.png",100,100,3),Is.Null);
            File.WriteAllText(path,"{}");
            Assert.That(NavalSpriteCatalog.Get("valid.png",100,100,4),Is.Not.Null,"Metadata is cached until reconnect");
            NavalSpriteCatalog.Clear();
            Assert.That(NavalSpriteCatalog.Get("valid.png",100,100,4),Is.Null);
        }
        finally
        {
            ClientConfiguration.ResourcesDirectory=previous;
            NavalSpriteCatalog.Clear();
            // Only files made by this test, in its unique temporary directory.
            foreach (var path in Directory.GetFiles(Path.Combine(directory,"ocean"))) File.Delete(path);
            Directory.Delete(Path.Combine(directory,"ocean")); Directory.Delete(directory);
        }
    }
}
