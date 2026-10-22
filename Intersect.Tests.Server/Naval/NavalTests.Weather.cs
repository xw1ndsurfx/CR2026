using Intersect.Framework.Core.GameObjects.Maps;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test] public void GlobalRandomWeatherLastsFifteenMinutesAndHasClearWeatherBeforeAndAfter()
    {
        var map=Guid.Parse("ee70bda6-e19f-4d7c-8336-16f802c261e1");
        var utc=new DateTimeOffset(2026,10,21,12,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds();
        for(var cycle=0;cycle<24;cycle++)
        {
            var window=MapWeatherCycle.Window(map,utc+cycle*MapWeatherCycle.CycleMilliseconds);
            Assert.That(window.End-window.Start,Is.EqualTo(15*60*1000));
            Assert.That(MapWeatherCycle.IsActive(map,window.Start-1),Is.False);
            Assert.That(MapWeatherCycle.IsActive(map,window.Start),Is.True);
            Assert.That(MapWeatherCycle.IsActive(map,window.End-1),Is.True);
            Assert.That(MapWeatherCycle.IsActive(map,window.End),Is.False);
            Assert.That(MapWeatherCycle.Window(map,window.Start+60000),Is.EqualTo(window));
        }
        Assert.That(MapWeatherCycle.Window(Guid.NewGuid(),utc),Is.EqualTo(MapWeatherCycle.Window(map,utc)));
    }
    [Test] public void OldMapsKeepContinuousWeatherAndNewSettingsRoundTripWithoutChangingAnimation()
    {
        var map=new MapDescriptor(Guid.NewGuid());
        map.Load(JsonConvert.SerializeObject(new {WeatherIntensity=75,WeatherYSpeed=4}));
        Assert.That(map.WeatherEffects.RandomWeather,Is.False);Assert.That(map.WeatherEffects.GroundSplashes,Is.False);
        Assert.That(map.WeatherIntensity,Is.EqualTo(75));
        var animation=Guid.NewGuid();
        map.WeatherEffects=new(){RandomWeather=true,Flashes=true,Lightning=true,GroundSplashes=true,SplashAnimationId=animation};
        var restored=new MapDescriptor(Guid.NewGuid()){WeatherEffectsJson=map.WeatherEffectsJson};
        Assert.That(restored.WeatherEffects.RandomWeather,Is.True);Assert.That(restored.WeatherEffects.SplashAnimationId,Is.EqualTo(animation));
        var packetMap=JsonConvert.DeserializeObject<MapDescriptor>(JsonConvert.SerializeObject(map));
        Assert.That(packetMap!.WeatherEffects.Flashes,Is.True);Assert.That(packetMap.WeatherEffects.Lightning,Is.True);
    }
}
