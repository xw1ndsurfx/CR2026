using Intersect.Client.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class WeatherFadeTests
{
    [Test]
    public void RemovingRainKeepsItsParticlesSoundAndEnvironmentUntilFadeCompletes()
    {
        var map = new MapDescriptor(Guid.NewGuid());
        var appearance = WeatherAppearance.From(map);
        var fade = new WeatherVisualTransition();
        Assert.That(fade.Update(appearance, 0), Is.Zero);
        Assert.That(fade.Update(appearance, 10000), Is.EqualTo(1));
        map.WeatherIntensity = 0;
        map.WeatherEffects.RainSound = "";
        map.WeatherEffects.OverrideRainBrightness = false;
        Assert.That(fade.Update(null, 15000), Is.EqualTo(.5f));
        Assert.That(fade.Appearance, Is.EqualTo(appearance));
        Assert.That(fade.Appearance!.Sound, Is.EqualTo("rain_medium.wav"));
        Assert.That(fade.Update(null, 20000), Is.Zero);
        Assert.That(fade.Appearance, Is.Null);
    }

    [Test]
    public void ReplacingGeneratedRainWithAnAnimationFadesOutBeforeChangingRenderer()
    {
        var generated = WeatherAppearance.From(new MapDescriptor(Guid.NewGuid()));
        var animated = generated with { Generated = false, AnimationId = Guid.NewGuid() };
        var fade = new WeatherVisualTransition();
        fade.Update(generated, 0); fade.Update(generated, 10000);
        Assert.That(fade.Update(animated, 15000), Is.EqualTo(.5f));
        Assert.That(fade.Appearance, Is.EqualTo(generated));
        Assert.That(fade.Update(animated, 20000), Is.Zero);
        Assert.That(fade.Appearance, Is.EqualTo(animated));
        Assert.That(fade.Update(animated, 25000), Is.EqualTo(.5f));
        Assert.That(fade.Update(animated, 30000), Is.EqualTo(1));
    }

    [Test]
    public void ReversalAndOutdoorMapTransfersContinueFromTheVisibleStrength()
    {
        var rain = WeatherAppearance.From(new MapDescriptor(Guid.NewGuid()));
        var first = new WeatherVisualTransition(); first.Update(rain, 0); first.Update(rain, 6000);
        first.Update(null, 8000);
        var next = new WeatherVisualTransition(); next.CopyFrom(first);
        Assert.That(next.Update(rain, 8000), Is.EqualTo(.4f).Within(.0001));
        Assert.That(next.Update(rain, 9000), Is.EqualTo(.5f).Within(.0001));
        Assert.That(next.Update(rain, 8500), Is.EqualTo(.5f).Within(.0001), "A clock reversal cannot jump opacity");
    }

    [Test]
    public void LiveFogOpacityMovesGraduallyInEitherDirectionWithoutOvershooting()
    {
        var opacity = WeatherTransition.Approach(150, 0, 255f / 5);
        Assert.That(opacity, Is.EqualTo(99));
        opacity = WeatherTransition.Approach(opacity, 200, 255f / 5);
        Assert.That(opacity, Is.EqualTo(150));
        Assert.That(WeatherTransition.Approach(opacity, 160, 255f / 5), Is.EqualTo(160));
        Assert.That(WeatherTransition.Approach(opacity, 0, 0), Is.EqualTo(opacity));
    }
}
