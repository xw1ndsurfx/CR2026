using Intersect.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class ClientConfigurationSafetyTests
{
    private string _directory = "";
    private string Config => Path.Combine(_directory, "config.json");
    [SetUp] public void CreateDirectory() { _directory = Path.Combine(Path.GetTempPath(), "config-regression-" + Guid.NewGuid()); Directory.CreateDirectory(_directory); }
    [TearDown] public void Cleanup() => Directory.Delete(_directory, true);

    [Test]
    public void RepeatedStartupDoesNotMultiplyMenuAnimationsOrChangeEndpoint()
    {
        var config = new ClientConfiguration { Host = "test.example", Port = 5462,
            MenuAnimations = [new() { AnimationId = Guid.NewGuid(), XPercent = 25 },new() { AnimationId = Guid.NewGuid(), XPercent = 75 }] };
        config.Save(Config);
        var expected = File.ReadAllText(Config);
        for (var i=0;i<12;i++)
        {
            ConfigurationHelper.LoadSafely(config,Config);
            Assert.That(config.MenuAnimations,Has.Count.EqualTo(2));
            Assert.That(config.Host,Is.EqualTo("test.example")); Assert.That(config.Port,Is.EqualTo(5462));
            Assert.That(File.ReadAllText(Config),Is.EqualTo(expected));
        }
    }

    [Test]
    public void InflatedLegacyArrayIsConsumedWithOnly32RulesMaterializedAndCompactSave()
    {
        using(var writer = new StreamWriter(Config))
        {
            writer.Write("{\"Host\":\"legacy.example\",\"Port\":5462,\"MenuAnimations\":[");
            for(var i=0;i<8192;i++) { if(i>0) writer.Write(','); writer.Write("{\"XPercent\":25,\"Upper\":{\"Sprite\":\"fireworks.png\",\"FrameCount\":15}}"); }
            writer.Write("]}");
        }
        var config = ConfigurationHelper.LoadSafely(new ClientConfiguration(),Config);
        Assert.That(config.MenuAnimations,Has.Count.EqualTo(32));
        Assert.That(config.MenuAnimations.All(m=>m.XPercent==25 && m.Upper?.Sprite=="fireworks.png"),Is.True);
        Assert.That(config.Host,Is.EqualTo("legacy.example")); Assert.That(config.Port,Is.EqualTo(5462));
        Assert.That(new FileInfo(Config).Length,Is.LessThan(100_000));
    }

    [Test]
    public void FailedLoadDoesNotOverwriteExistingInvalidFile()
    {
        const string corrupt = "{\"Host\":\"preserve.example\",\"MenuAnimations\":[";
        File.WriteAllText(Config,corrupt);
        ConfigurationHelper.LoadSafely(new ClientConfiguration(),Config);
        Assert.That(File.ReadAllText(Config),Is.EqualTo(corrupt));
    }

    [Test]
    public void SerializationFailureKeepsOriginalAndRemovesTemporaryFile()
    {
        File.WriteAllText(Config,"original");
        Assert.Throws<JsonSerializationException>(()=>ConfigurationHelper.Save(new BrokenConfiguration(),Config));
        Assert.That(File.ReadAllText(Config),Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory),Has.Length.EqualTo(1));
    }

    [Test]
    public void ExclusiveFileLockKeepsPreviousConfigurationIntact()
    {
        if(!OperatingSystem.IsWindows()) Assert.Ignore("Windows exclusive file sharing regression");
        File.WriteAllText(Config,"original");
        using(var locked = new FileStream(Config,FileMode.Open,FileAccess.Read,FileShare.None))
            Assert.Throws<IOException>(()=>new ClientConfiguration().Save(Config));
        Assert.That(File.ReadAllText(Config),Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(_directory),Has.Length.EqualTo(1));
    }

    [Test]
    public void MissingFileIsStillInitializedAndSaveBoundsRuntimeList()
    {
        var config = new ClientConfiguration { MenuAnimations = Enumerable.Range(0,100).Select(_=>new MenuAnimationDefinition()).ToList() };
        ConfigurationHelper.LoadSafely(config,Config);
        Assert.That(File.Exists(Config),Is.True);
        var loaded = new ClientConfiguration().Load(Config);
        Assert.That(loaded.MenuAnimations,Has.Count.EqualTo(32));
    }

    [Test, Explicit("Set CLIENT_CONFIG_REPRO_READONLY to audit a copied legacy configuration without writing it.")]
    public void ActualLegacyConfigurationCanBeReadWithoutRewritingIt()
    {
        var path = Environment.GetEnvironmentVariable("CLIENT_CONFIG_REPRO_READONLY");
        if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("No legacy configuration provided.");
        var before = new FileInfo(path!); var length = before.Length; var modified = before.LastWriteTimeUtc;
        var config = new ClientConfiguration().Load(path);
        Assert.That(config.MenuAnimations.Count,Is.LessThanOrEqualTo(32));
        var after = new FileInfo(path!);
        Assert.That(after.Length,Is.EqualTo(length)); Assert.That(after.LastWriteTimeUtc,Is.EqualTo(modified));
        TestContext.WriteLine($"Read-only legacy configuration: {length} bytes, {config.MenuAnimations.Count} materialized rules.");
    }

    private sealed class BrokenConfiguration : IConfiguration<BrokenConfiguration>
    {
        public string Broken => throw new IOException("simulated serialization error");
        public BrokenConfiguration Load(string path,bool failQuietly=false) => this;
        public BrokenConfiguration Save(string path,bool failQuietly=false) => ConfigurationHelper.Save(this,path,failQuietly);
    }
}
