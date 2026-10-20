using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    private static string ResourceRoot()
    {
        var path = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (path != null && !File.Exists(Path.Combine(path.FullName, "Intersect.sln"))) path = path.Parent;
        return Path.Combine(path?.FullName ?? throw new DirectoryNotFoundException("Repository resources"), "resources", "ocean");
    }

    [TestCase("Ship_CorpsRoyaux_002.png")]
    [TestCase("Ship_CorpsRoyaux_005.png")]
    [TestCase("Ship_CorpsRoyaux_001_sink.png")]
    [TestCase("Ship_CorpsRoyaux_002_Sink.png")]
    [TestCase("Ship_CorpsRoyaux_003_sink.png")]
    [TestCase("Ship_CorpsRoyaux_004_sink.png")]
    [TestCase("Ship_CorpsRoyaux_005_sink.png")]
    [TestCase("Ship_CorpsRoyaux_006_sink.png")]
    [TestCase("Ship_Herbonia_001_sink.png")]
    [TestCase("Ship_Herbonia_002_sink.png")]
    public void OceanAtlasMatchesOriginalPngAndKeepsEveryPoseInBounds(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(ResourceRoot(), name));
        var json = File.ReadAllText(Path.Combine(ResourceRoot(), name + ".naval.json"));
        var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(json)!;
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        Assert.That(atlas.IsValid(width, height, 4), Is.True);
        Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Is.EqualTo(Newtonsoft.Json.Linq.JObject.Parse(json)["SourceSha256"]!.ToString()));
        for (var direction = 0; direction < 8; direction++)
            for (var frame = 0; frame < 4; frame++)
                Assert.That(atlas.Frame(direction * 45, frame * 180, 180), Is.SameAs(atlas.Directions[direction][frame]));
        var sinking = name.EndsWith("_sink.png",StringComparison.OrdinalIgnoreCase);
        Assert.That(atlas.Loop,Is.EqualTo(!sinking));
        Assert.That(atlas.Frame(359, 720, 180), Is.SameAs(atlas.Directions[0][sinking ? 3 : 0]));
        Assert.That(atlas.FrameIndex(-100,180),Is.Zero);
        Assert.That(atlas.IsFinished(719,180),Is.False);
        Assert.That(atlas.IsFinished(720,180),Is.EqualTo(sinking));
        if (sinking) Assert.That(atlas.FrameIndex(long.MaxValue,180),Is.EqualTo(3));
        Assert.That(atlas.IsValid(width + 1, height, 4), Is.False);
        Assert.That(atlas.IsValid(width, height, 3), Is.False);
    }

    [Test]
    public void EverySinkingBindingSelectsAnExistingOneShotAtlas()
    {
        var bindings = JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(
            Path.Combine(ResourceRoot(),"ships.sinking.json")))!;
        Assert.That(bindings,Has.Count.EqualTo(8));
        foreach (var entry in bindings)
        {
            Assert.That(Path.GetFileName(entry.Key),Is.EqualTo(entry.Key));
            Assert.That(File.Exists(Path.Combine(ResourceRoot(),entry.Value)),Is.True);
            var atlas=JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(
                Path.Combine(ResourceRoot(),entry.Value+".naval.json")))!;
            Assert.That(atlas.Loop,Is.False);
        }
    }

    [Test]
    public void EveryDistributionBundleIncludesTheOceanAssetsAndMetadata()
    {
        var repository = Directory.GetParent(Directory.GetParent(ResourceRoot())!.FullName)!.FullName;
        var paths = Directory.GetFiles(Path.Combine(repository,".github","bundles"),"*.json");
        Assert.That(paths,Has.Length.EqualTo(9));
        foreach (var path in paths)
        {
            var bundle = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            Assert.That(bundle["includes"]!.Any(entry => entry["source"]?.ToString()=="resources/ocean/**" &&
                entry["target"]?.ToString()=="Client and Editor/resources/ocean"),Is.True,path);
        }
    }

    [Test]
    public void OceanAtlasRejectsMissingDirectionsInvalidAnchorsAndOverflowingRectangles()
    {
        var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(
            Path.Combine(ResourceRoot(), "Ship_CorpsRoyaux_002.png.naval.json")))!;
        atlas.Directions[1] = null!;
        Assert.That(atlas.IsValid(1448, 1086, 4), Is.False);
        atlas.Directions[1] = atlas.Directions[0];
        var frame = atlas.Directions[0][0];
        frame.X = int.MaxValue;
        Assert.That(atlas.IsValid(1448, 1086, 4), Is.False);
        frame.X = 0; frame.AnchorX = double.NaN;
        Assert.That(atlas.IsValid(1448, 1086, 4), Is.False);
        frame.AnchorX = 10; atlas.PixelsPerTile = 0;
        Assert.That(atlas.IsValid(1448, 1086, 4), Is.False);
    }
}
