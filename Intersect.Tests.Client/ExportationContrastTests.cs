using System.Reflection;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.Exportation;
using NUnit.Framework;

namespace Intersect.Tests.Client;

/// <summary>
/// Guards the night-independent Exportation UI treatment.
/// Pixel appearance still requires a day/night smoke test with actual art.
/// </summary>
[TestFixture]
public sealed class ExportationContrastTests
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void WorldBackdropMayNotReturnToNearlyBlackNightOverlay()
    {
        var dimmer = typeof(ExportationWindow).GetField("WorldDimOpacity", Hidden);
        Assert.That(dimmer, Is.Not.Null);
        Assert.That((byte)dimmer!.GetRawConstantValue()!, Is.InRange((byte)0, (byte)100),
            "The world dimmer must stay subtle; the UI has its own opaque frame.");
    }

    [Test]
    public void LocalBackingAndContrastPaintArePresent()
    {
        var window = typeof(ExportationWindow);
        foreach (var name in new[]
        {
            "WoodFrame", "ParchmentBacking", "CrateBacking", "DeliveryBacking",
            "WarmParchmentWash", "WarmWoodWash",
        })
            Assert.That(window.GetField(name, Hidden), Is.Not.Null,
                $"Missing Exportation night-independent contrast layer: {name}");

        Assert.Multiple(() =>
        {
            Assert.That(window.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(window.GetMethod("FillScene", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(window.GetMethod("PaintAction", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        });
    }

    [TestCase(10, 28, 2.8f)]
    [TestCase(10, 17, 1.7f)]
    [TestCase(12, 24, 2f)]
    public void SingleSizePixelFontKeepsOriginalDesignTextSize(
        int atlasSize, int desiredPixelSize, float expectedScale)
    {
        var font = new TestBitmapFont(atlasSize);
        var (native, scaling) = ExportationWindow.ResolvePixelFont(font, desiredPixelSize);
        Assert.Multiple(() =>
        {
            Assert.That(native, Is.EqualTo(atlasSize));
            Assert.That(scaling, Is.EqualTo(expectedScale).Within(.0001f),
                "A 10px Empire atlas must be magnified instead of silently drawing 28px headings at 10px.");
            Assert.That(native * scaling, Is.EqualTo(desiredPixelSize).Within(.0001f));
        });
    }

    [Test]
    public void ExactAtlasSizeDoesNotUnnecessarilyScaleText()
    {
        var font = new TestBitmapFont(12, 16, 20, 28);
        var (native, scaling) = ExportationWindow.ResolvePixelFont(font, 28);
        Assert.That(native, Is.EqualTo(28));
        Assert.That(scaling, Is.EqualTo(1f));
    }

    [TestCase(40, 16, 2.5f)]
    [TestCase(18, 16, 1.125f)]
    [TestCase(28, 32, 0.875f)]
    [TestCase(16, 48, 0.35f)]
    public void ExportationHeadingsScaleUsingActualRenderedHeightEvenWithGlobalFontOverride(
        int desiredHeight, int measuredHeight, float expectedScale)
    {
        var scale = ExportationWindow.ScaleForMeasuredFont(desiredHeight, measuredHeight);
        Assert.That(scale, Is.EqualTo(expectedScale).Within(.001f),
            "Bitmap glyphs must be measured after global UI font size overrides.");
    }

    [Test]
    public void BitmapTextFittingUsesVisibleInkInsteadOfTransparentCellPadding()
    {
        var sparse = ExportationWindow.ScaleForVisibleInk(16, 16, new FontInkMetrics(3f / 16, 7f / 16));
        var full = ExportationWindow.ScaleForVisibleInk(16, 16, new FontInkMetrics(0, 1));
        Assert.That(sparse * 7, Is.EqualTo(12).Within(.001));
        Assert.That(sparse, Is.GreaterThan(full), "Empire's 7px glyph must not be treated as a 16px visible letter.");
    }

    [Test]
    public void CrateCountsMatchActualPackedItemsByGroup()
    {
        var wood = Guid.NewGuid();
        var potion = Guid.NewGuid();
        var provisions = Guid.NewGuid();
        var route = new ExportationRoute
        {
            Lines =
            [
                new() { ItemId = wood, Group = CargoGroup.Materials, Quantity = 5 },
                new() { ItemId = potion, Group = CargoGroup.Fragile, Quantity = 2 },
                new() { ItemId = provisions, Group = CargoGroup.Provisions, Quantity = 7 },
            ],
        };
        var run = new ExportationRun
        {
            Packed = new Dictionary<Guid, int>
            {
                [wood] = 4,
                [potion] = 2,
                [provisions] = 3,
            },
        };
        var counts = ExportationWindow.CountCrates(route, run);
        Assert.Multiple(() =>
        {
            Assert.That(counts.MaterialsPacked, Is.EqualTo(4));
            Assert.That(counts.MaterialsTotal, Is.EqualTo(5));
            Assert.That(counts.OtherPacked, Is.EqualTo(5));
            Assert.That(counts.OtherTotal, Is.EqualTo(9));
        });
    }

    [Test]
    public void VoyageInstructionsNeverPretendThePlayerDockedEarly()
    {
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.Preparing,
            false, false)[1], Is.EqualTo("2. Seal the cargo"));
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.InTransit,
            false, false)[1], Is.EqualTo("2. Board your ship"));
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.InTransit,
            true, false)[1], Is.EqualTo("2. Sailing to destination"));
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.InTransit,
            true, true)[1], Is.EqualTo("2. Sea arrival confirmed"));
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.Delivered,
            true, true)[2], Is.EqualTo("3. Rewards received"));
        Assert.That(ExportationWindow.DeliveryStepKeys(DeliveryStage.Lost,
            false, false)[0], Is.EqualTo("1. Cargo lost"));
    }

    [Test]
    public void HelpIsAnActualInteractiveControlRatherThanArtwork()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = typeof(ExportationWindow);
        foreach (var field in new[]
        {
            "_helpOverlay", "_helpButton", "_helpClose", "_materialCrate",
            "_otherCrate", "_step2", "_step3", "_shelfSummary",
        })
            Assert.That(window.GetField(field, flags), Is.Not.Null,
                $"Missing real Exportation UI control {field}");
    }

    [Test]
    public void ExportationHelpIsLocalized()
    {
        Assert.That(ExportationText.Get("How Exportation works", "fr"),
            Is.EqualTo("Comment fonctionne l’Exportation"));
        Assert.That(ExportationText.Get("Close help", "es"),
            Is.EqualTo("Cerrar ayuda"));
    }

    private sealed class TestBitmapFont(params int[] sizes) : IFont
    {
        public string Name => "Empire";
        public ICollection<int> SupportedSizes { get; } = sizes;
        public int PickBestMatchFor(int size) =>
            SupportedSizes.OrderBy(supported => Math.Abs(size - supported)).First();
        public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize;
    }

}
