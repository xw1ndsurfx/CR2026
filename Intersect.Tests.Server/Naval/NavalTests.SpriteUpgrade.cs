using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void LegacySoutheastMetadataIsUpgradedOnceAndCustomAtlasesAreKept()
    {
        var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(Path.Combine(ResourceRoot(), "Ship_CorpsRoyaux_002.png.naval.json")))!;
        atlas.Version = 1;
        atlas.Directions[3] = atlas.Directions[7]; // old installed atlas used a stern-facing pose here.
        var north = atlas.Directions[0]; var southwest = atlas.Directions[5];
        Assert.That(atlas.UpgradeLegacySoutheast("custom.png"), Is.False);
        Assert.That(atlas.UpgradeLegacySoutheast("Ship_CorpsRoyaux_002.png"), Is.True);
        Assert.That(atlas.Directions[0], Is.SameAs(north));
        Assert.That(atlas.Directions[5], Is.SameAs(southwest));
        Assert.That(atlas.Frame(135, 0, 180).FlipHorizontal, Is.True);
        Assert.That(atlas.Frame(135, 0, 180).X, Is.EqualTo(atlas.Frame(225, 0, 180).X));
        Assert.That(atlas.UpgradeLegacySoutheast("Ship_CorpsRoyaux_002.png"), Is.False);
    }

    [Test, Explicit("Read-only verification of an installed legacy atlas; NAVAL_LEGACY_ATLAS must name that file.")]
    public void InstalledLegacySoutheastCanBeCorrectedWithoutRewritingResources()
    {
        var path = Environment.GetEnvironmentVariable("NAVAL_LEGACY_ATLAS")!;
        var source = File.ReadAllText(path); var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(source)!;
        Assert.That(atlas.Frame(135, 0, 180).FlipHorizontal, Is.False);
        Assert.That(atlas.UpgradeLegacySoutheast(Path.GetFileName(path).Replace(".naval.json", "")), Is.True);
        Assert.That(atlas.Frame(135, 0, 180).FlipHorizontal, Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo(source));
    }
}
