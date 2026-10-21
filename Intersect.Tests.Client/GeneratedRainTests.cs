using Intersect.Client.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class GeneratedRainTests
{
    [Test]
    public void RainNeedsNoAnimationAndOldMapsKeepItDisabled()
    {
        var legacy = JsonConvert.DeserializeObject<MapWeatherEffects>("{}")!;
        Assert.That(legacy.GeneratedRain, Is.False);
        var map = new MapDescriptor(Guid.NewGuid());
        map.WeatherEffects.GeneratedRain = true;
        map.WeatherEffects.RandomWeather = true;
        map.WeatherEffects.GroundSplashes = true;
        var restored = new MapDescriptor(Guid.NewGuid()) { WeatherEffectsJson = map.WeatherEffectsJson };
        Assert.That(restored.WeatherEffects.GeneratedRain, Is.True);
        Assert.That(restored.WeatherEffects.RandomWeather, Is.True);
        Assert.That(restored.WeatherEffects.GroundSplashes, Is.True);
        var network = JsonConvert.DeserializeObject<MapDescriptor>(JsonConvert.SerializeObject(map))!;
        Assert.That(network.WeatherEffects.GeneratedRain, Is.True);
        Assert.That(network.WeatherAnimationId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void DensityIsBoundedAndClearsWhenRainStops()
    {
        var rain = new GeneratedRainSimulation(new Random(1));
        rain.Update(0, 0, 1920, 1080, 20, 0, 0, 0, null);
        var lightCount = rain.Drops.Count;
        rain.Update(0, 0, 1920, 1080, 100, 0, 0, 0, null);
        Assert.That(rain.Drops.Count, Is.GreaterThan(lightCount));
        rain.Update(0, 0, 16000, 16000, int.MaxValue, int.MaxValue, int.MinValue, 10, null);
        Assert.That(rain.Drops.Count, Is.EqualTo(GeneratedRainSimulation.MaximumDrops));
        Assert.That(rain.Drops.All(d => float.IsFinite(d.X) && float.IsFinite(d.Y)), Is.True);
        rain.Update(0, 0, 1920, 1080, 0, 0, 0, .1f, null);
        Assert.That(rain.Drops, Is.Empty);
    }

    [Test]
    public void DefaultSpeedFallsAndImpactsStayOnScreenAfterCameraMoves()
    {
        var rain = new GeneratedRainSimulation(new Random(5));
        var impacts = new List<(float X, float Y)>();
        rain.Update(0, 0, 1280, 720, 80, 2, 0, 0, null);
        var first = rain.Drops[0];
        var x = first.X; var y = first.Y;
        rain.Update(0, 0, 1280, 720, 80, 2, 0, .001f, null);
        Assert.That(first.Y, Is.GreaterThan(y));
        Assert.That(first.X, Is.GreaterThan(x));
        for (var i = 0; i < 120; i++)
            rain.Update(10000, 20000, 1280, 720, 80, -2, 0, 1 / 60f,
                (ix, iy) => impacts.Add((ix, iy)));
        Assert.That(impacts, Is.Not.Empty);
        Assert.That(impacts.All(p => p.X >= 10000 && p.X <= 11280 && p.Y >= 20000 && p.Y <= 20720), Is.True);
        rain.Clear();
        Assert.That(rain.Drops, Is.Empty);
    }

    [Test]
    public void FallingMotionUsesElapsedTimeRatherThanFrameCount()
    {
        var first = new GeneratedRainSimulation(new Random(8));
        var second = new GeneratedRainSimulation(new Random(8));
        first.Update(0, 0, 1280, 720, 1, 1, 0, 0, null);
        second.Update(0, 0, 1280, 720, 1, 1, 0, 0, null);
        first.Update(0, 0, 1280, 720, 1, 1, 0, .01f, null);
        second.Update(0, 0, 1280, 720, 1, 1, 0, .005f, null);
        second.Update(0, 0, 1280, 720, 1, 1, 0, .005f, null);
        Assert.That(first.Drops[0].X, Is.EqualTo(second.Drops[0].X).Within(.001));
        Assert.That(first.Drops[0].Y, Is.EqualTo(second.Drops[0].Y).Within(.001));
    }
}
