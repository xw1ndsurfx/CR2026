using System.Reflection;
using Intersect.Client.Interface.Game;
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
}
