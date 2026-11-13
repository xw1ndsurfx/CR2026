using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Framework.Core.QualityOfLife;
using NUnit.Framework;
using MessagePack;
using Intersect.Client.MiniGames;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class PotionAssetsTests
{
    [Test]
    public void LegacyAndNullConfigurationsReceiveTheDefaultPack()
    {
        Assert.That(RewardConfiguration.FromJson("{}").PotionAssets, Is.EqualTo(new PotionPresentationAssets()));
        Assert.That(RewardConfiguration.FromJson("{\"PotionAssets\":null}").PotionAssets.IsValid, Is.True);
    }

    [Test]
    public void CustomImagesSoundsAndExplicitEmptySelectionsSurviveTheWireJson()
    {
        var assets = new PotionPresentationAssets { Background = "my_workshop.png", PieceAtlas = "",
            RotateSound = "my_rotate.wav", DropSound = "", MergeSound = "custom_merge.wav", CompleteSound = "success.wav", GameOverSound = "failure.wav" };
        var result = RewardConfiguration.FromJson(new RewardConfiguration { PotionAssets = assets }.ToJson());
        Assert.That(result.PotionAssets, Is.EqualTo(assets));
        Assert.That(result.IsStructurallyValid, Is.True);
    }

    [Test]
    public void ServerStateCarriesCustomAssetsThroughMessagePack()
    {
        var assets = new PotionPresentationAssets { Background = "custom.png", MergeSound = "fusion.wav", DropSound = "" };
        var state = new PotionSessionState { Assets = assets };
        var received = MessagePackSerializer.Deserialize<PotionSessionState>(MessagePackSerializer.Serialize(state));
        Assert.That(received.Assets, Is.EqualTo(assets));
    }

    [Test]
    public void OlderPacketsWithoutTheAppendedAssetFieldUseTheDefaultPack()
    {
        var bytes = MessagePackSerializer.Serialize(new PotionSessionState());
        var reader = new MessagePackReader(bytes);
        var count = reader.ReadArrayHeader();
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(count - 1);
        for (var i = 0; i < count - 1; ++i) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var received = MessagePackSerializer.Deserialize<PotionSessionState>(buffer.WrittenMemory);
        Assert.That(received.Assets, Is.EqualTo(new PotionPresentationAssets()));
    }

    [TestCase("../other.png")]
    [TestCase("C:\\other.png")]
    [TestCase("folder/other.png")]
    [TestCase("wrong.wav")]
    [TestCase("bad\n.png")]
    public void InvalidImagePathsCannotBeDistributed(string file)
    {
        Assert.Throws<InvalidDataException>(() => RewardConfiguration.FromJson(
            new RewardConfiguration { PotionAssets = new() { Background = file } }.ToJson()));
    }

    [Test]
    public void InvalidSoundAndNullFilenamesAreRejected()
    {
        Assert.That(new PotionPresentationAssets { RotateSound = "../sound.wav" }.IsValid, Is.False);
        Assert.That(new PotionPresentationAssets { PieceAtlas = null! }.IsValid, Is.False);
    }

    [Test]
    public void ResultFeedbackUsesConfiguredSoundsAndNeverReplaysSnapshots()
    {
        var assets = new PotionPresentationAssets { MergeSound = "fuse.wav", CompleteSound = "win.wav", GameOverSound = "lose.wav" };
        var before = new PotionSessionState { Revision = 1, Board = [1], Assets = assets };
        var merged = new PotionSessionState { Revision = 2, Board = [2], LastScoreGain = 10, Assets = assets };
        Assert.That(PotionResultSound.Select(null, merged), Is.Empty);
        Assert.That(PotionResultSound.Select(merged, merged), Is.Empty);
        Assert.That(PotionResultSound.Select(before, merged), Is.EqualTo("fuse.wav"));
        merged.Complete = true;
        Assert.That(PotionResultSound.Select(before, merged), Is.EqualTo("win.wav"));
        merged.Complete = false; merged.GameOver = true;
        Assert.That(PotionResultSound.Select(before, merged), Is.EqualTo("lose.wav"));
        merged.Assets = assets with { GameOverSound = "" };
        Assert.That(PotionResultSound.Select(before, merged), Is.Empty);
    }

    [TestCase("fr")]
    [TestCase("es")]
    public void EditorPresentationLabelsHaveTranslations(string language)
    {
        foreach (var source in new[] { "Artwork and sounds", "Workshop background", "Ingredient atlas (4 x 4)", "Rotate ingredients",
            "Drop ingredients", "Ingredient fusion", "Recipe complete", "Board full" })
            Assert.That(QualityOfLifeText.Get(source, language), Is.Not.EqualTo(source), source);
    }
}
