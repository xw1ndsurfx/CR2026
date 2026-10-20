using Intersect.Client.Naval;
using Intersect.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class NavalSpriteCatalogTests
{
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
