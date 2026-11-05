using System.Reflection;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Graphics;
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

    private sealed class TestBitmapFont(params int[] sizes) : IFont
    {
        public string Name => "Empire";
        public ICollection<int> SupportedSizes { get; } = sizes;
        public int PickBestMatchFor(int size) =>
            SupportedSizes.OrderBy(supported => Math.Abs(size - supported)).First();
        public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize;
    }

}
