using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.Pvp;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Network.Packets.WorldEvents;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public class WorldActivitiesRoundTripTests
{
    [Test]
    public void DefaultConfigurationCanBeOpenedClonedAndSavedRepeatedly()
    {
        var config = new WorldActivitiesConfiguration();
        for (var cycle = 0; cycle < 5; cycle++)
        {
            config = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(JsonConvert.SerializeObject(config))!;
            Assert.That(config.Pvp.Grades, Has.Count.EqualTo(10));
            Assert.That(config.IsValid, Is.True, "The server must accept its own snapshot after editing.");
        }
    }

    [TestCase(MetalworkKind.Smelting)]
    [TestCase(MetalworkKind.Smithing)]
    public void ValidWorkshopRecipeSurvivesEditorAndServerClone(MetalworkKind kind)
    {
        var config = new WorldActivitiesConfiguration();
        config.Pvp.Grades[1].Name = "Custom grade";
        config.Metalworking.Recipes.Add(new() { Kind = kind, Enabled = true, OutputItemId = Guid.NewGuid(),
            Ingredients = [new() { ItemId = Guid.NewGuid(), Quantity = 12 }] });
        var edited = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(JsonConvert.SerializeObject(config))!;
        var merged = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(JsonConvert.SerializeObject(config))!;
        merged.ApplySection(edited, WorldActivitiesSection.Metalworking);
        Assert.That(merged.IsValid, Is.True);
        Assert.That(merged.Pvp.Grades[1].Name, Is.EqualTo("Custom grade"));
        Assert.That(merged.Metalworking.Recipes, Has.Count.EqualTo(1));
        Assert.That(merged.Metalworking.Recipes[0].Ingredients[0].Quantity, Is.EqualTo(12));
    }

    [Test]
    public void InvalidRankThresholdsAreStillRejected()
    {
        var config = new PvpConfiguration();
        config.Grades[1].Points = 0;
        var copy = JsonConvert.DeserializeObject<PvpConfiguration>(JsonConvert.SerializeObject(config))!;
        Assert.That(copy.Grades, Has.Count.EqualTo(10));
        Assert.That(copy.IsValid, Is.False);
    }

    [Test]
    public void ArenaAndSaveReceiptRoundTripPreserveIdentityAndError()
    {
        var config = new WorldActivitiesConfiguration();
        config.Pvp.Arenas.Add(new() { Enabled = true, MapId = Guid.NewGuid() });
        var copy = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(JsonConvert.SerializeObject(config))!;
        Assert.That(copy.IsValid, Is.True);
        Assert.That(copy.Pvp.Arenas[0].Id, Is.EqualTo(config.Pvp.Arenas[0].Id));
        var request = new SaveWorldActivitiesPacket { Json = JsonConvert.SerializeObject(config), Section = WorldActivitiesSection.Pvp, RequestId = Guid.NewGuid() };
        var decoded = MessagePackSerializer.Deserialize<SaveWorldActivitiesPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(decoded.RequestId, Is.EqualTo(request.RequestId));
        var receipt = new WorldActivitiesPacket { RequestId = request.RequestId, Section = request.Section, Error = "test refusal" };
        var reply = MessagePackSerializer.Deserialize<WorldActivitiesPacket>(MessagePackSerializer.Serialize(receipt));
        Assert.That(reply.RequestId, Is.EqualTo(request.RequestId));
        Assert.That(reply.Saved, Is.False);
        Assert.That(reply.Error, Is.EqualTo("test refusal"));
    }

    [Test]
    public void LegacyEditorRequestWithoutReceiptFieldsStillDecodes()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write("{}");
        writer.Write((int)WorldActivitiesSection.Pvp);
        writer.Flush();
        var request = MessagePackSerializer.Deserialize<SaveWorldActivitiesPacket>(buffer.WrittenMemory);
        Assert.That(request.RequestId, Is.EqualTo(Guid.Empty));
        Assert.That(request.Section, Is.EqualTo(WorldActivitiesSection.Pvp));
    }
}
