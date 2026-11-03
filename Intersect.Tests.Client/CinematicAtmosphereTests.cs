using Intersect.Client.Core;
using NUnit.Framework;

namespace Intersect.Tests.Client;

// These are compositing math regressions, not a GPU or production-map capture.
// The ordinary client blends the ambient tint into a white light map, then
// multiplies the scene by that map. Perspective must preserve that same meaning.
[TestFixture]
public sealed class CinematicAtmosphereTests
{
    [TestCase(0)]
    [TestCase(64)]
    [TestCase(128)]
    [TestCase(255)]
    public void WhiteAmbientTintIsAnIdentityAtEveryOpacity(int alpha)
    {
        var light = Graphics.CinematicAmbientLight(new Color(alpha, 255, 255, 255));
        Assert.That(light, Is.EqualTo(Color.White),
            "White in the native light map leaves the terrain visible; it must never become a white screen overlay.");
    }

    [TestCase(0, 255)]
    [TestCase(128, 127)]
    [TestCase(255, 0)]
    public void BlackAmbientTintRetainsNativeDarknessAndNeutralEndpoints(int alpha, int expected)
    {
        var light = Graphics.CinematicAmbientLight(new Color(alpha, 0, 0, 0));
        Assert.That(light, Is.EqualTo(new Color(255, expected, expected, expected)));
    }

    [TestCase(0, 0)]
    [TestCase(10, 25)]
    [TestCase(25, 63)]
    [TestCase(50, 127)]
    [TestCase(75, 191)]
    [TestCase(90, 229)]
    [TestCase(100, 255)]
    public void BrightnessPercentageUsesTheNativeTruncatedOpaqueGrayscaleMultiplier(int brightness, int expected)
    {
        // Indoor brightness becomes a byte before the native additive light pass;
        // unlike color-tint compositing, this percentage conversion truncates.
        var light = Graphics.CinematicAmbientLight(brightness);
        Assert.That(light, Is.EqualTo(new Color(255, expected, expected, expected)));
    }

    [TestCase(128, 255, 64, 0, 255, 159, 127)]
    [TestCase(128, 48, 96, 200, 151, 175, 227)]
    [TestCase(255, 40, 80, 120, 40, 80, 120)]
    public void ColoredAmbientTintMatchesBlendingOnWhiteThenMultiplyingTheScene(
        int alpha, int red, int green, int blue, int expectedRed, int expectedGreen, int expectedBlue
    )
    {
        var tint = new Color(alpha, red, green, blue);
        var factor = Graphics.CinematicAmbientLight(tint);
        Assert.That(factor, Is.EqualTo(new Color(255, expectedRed, expectedGreen, expectedBlue)));

        // Reconstruct the two native blend operations independently using
        // normalized opacity, including the light texture's byte quantization.
        var referenceLightMap = SourceOver(tint, Color.White);
        foreach (var scene in new[] { new Color(255, 60, 120, 40), new Color(255, 15, 75, 160), Color.Black })
        {
            Assert.That(Multiply(scene, factor), Is.EqualTo(Multiply(scene, referenceLightMap)),
                "The ambient color belongs in a multiplicative light map, not a replacement color over the world.");
        }
    }

    [Test]
    public void GreenTerrainRemainsGreenUnderWhiteDaylightBeforeFogIsComposited()
    {
        var terrain = new Color(255, 50, 125, 35);
        var beforeFog = Multiply(terrain, Graphics.CinematicAmbientLight(Color.White));
        Assert.That(beforeFog, Is.EqualTo(terrain),
            "Regression for the gray cinematic screenshot: opaque white daylight must not erase the forest colors.");

        // A representative semitransparent fog still retains the terrain's
        // color contrast. This does not change or cap the authored fog opacity.
        var withFog = SourceOver(new Color(150, 180, 180, 180), beforeFog);
        Assert.Multiple(() =>
        {
            Assert.That(withFog.G, Is.GreaterThan(withFog.R));
            Assert.That(withFog.G, Is.GreaterThan(withFog.B));
        });
    }

    [Test]
    public void AmbientConversionDoesNotMutateTheNativeTint()
    {
        var tint = new Color(128, 48, 96, 200);
        var original = new Color(tint);
        _ = Graphics.CinematicAmbientLight(tint);
        Assert.That(tint, Is.EqualTo(original));
    }

    private static Color SourceOver(Color source, Color opaqueDestination)
    {
        var opacity = source.A / 255d;
        int Blend(byte value, byte background) => (int)Math.Round(value * opacity + background * (1 - opacity));
        return new Color(255, Blend(source.R, opaqueDestination.R), Blend(source.G, opaqueDestination.G),
            Blend(source.B, opaqueDestination.B));
    }

    private static Color Multiply(Color scene, Color light) => new(scene.A,
        (int)Math.Round(scene.R * light.R / 255d),
        (int)Math.Round(scene.G * light.G / 255d),
        (int)Math.Round(scene.B * light.B / 255d));
}
