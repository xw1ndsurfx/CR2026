using Intersect.Client.Entities;
using Intersect.Client.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;
[TestFixture]
public sealed class WeatherEnvironmentTests
{
    [Test]
    public void NewMapDefaultsMatchTheRequestedRainAndLegacyJsonKeepsOldBehavior()
    {
        var map=new MapDescriptor(Guid.NewGuid());var effects=map.WeatherEffects;
        Assert.That(effects.GeneratedRain && effects.RandomWeather && effects.Flashes && effects.Lightning && effects.GroundSplashes,Is.True);
        Assert.That(map.WeatherIntensity,Is.EqualTo(50));Assert.That(map.WeatherXSpeed,Is.Zero);Assert.That(map.WeatherYSpeed,Is.Zero);
        Assert.That(effects.FlashOpacity,Is.EqualTo(25));Assert.That(effects.PuddleDryMinutes,Is.EqualTo(3));
        Assert.That(effects.RainSound,Is.EqualTo("rain_medium.wav"));Assert.That(effects.ThunderSound,Is.EqualTo("thunder_crack_3.wav"));
        map.Load("{\"WeatherIntensity\":75,\"WeatherYSpeed\":4}");
        Assert.That(map.WeatherEffects.RandomWeather,Is.False);Assert.That(map.WeatherEffects.GeneratedRain,Is.False);
        Assert.That(map.WeatherIntensity,Is.EqualTo(75));Assert.That(map.WeatherYSpeed,Is.EqualTo(4));
    }
    [Test]
    public void ForcedRainRespectsIndoorMapsAndConfiguredRainAndRestoresNormalBrightness()
    {
        var map=new MapDescriptor(Guid.NewGuid()){Brightness=95};
        var window=MapWeatherCycle.Window(map.Id,MapWeatherCycle.CycleMilliseconds*5);
        var now=window.Start-60000;
        map.WeatherEffects.OverrideRainBrightness=true;map.WeatherEffects.RainBrightness=65;
        Assert.That(map.RainActive(now),Is.False);
        Assert.That(map.RainActive(now,now+900000),Is.True);
        Assert.That(map.RainAdjustedBrightness(map.Brightness,now,now+900000),Is.EqualTo(65));
        Assert.That(map.RainAdjustedBrightness(map.Brightness,now,now-1),Is.EqualTo(95));
        map.IsIndoors=true;
        Assert.That(map.WeatherActive(window.Start+1000,now+900000),Is.False);
        Assert.That(map.RainAdjustedBrightness(map.Brightness,now,now+900000),Is.EqualTo(95));
        map.IsIndoors=false;map.WeatherIntensity=0;
        Assert.That(map.RainActive(now,now+900000),Is.False);
    }
    [Test]
    public void WeatherFogAndBrightnessSettingsRoundTripIndependentlyOfNormalMapSettings()
    {
        var map=new MapDescriptor(Guid.NewGuid()){Fog="normal",FogTransparency=33,Brightness=93};
        map.WeatherEffects.OverrideRainFog=true;map.WeatherEffects.RainFog="storm";map.WeatherEffects.RainFogOpacity=155;
        map.WeatherEffects.RainFogXSpeed=-2;map.WeatherEffects.OverrideRainBrightness=true;map.WeatherEffects.RainBrightness=60;
        var copy=JsonConvert.DeserializeObject<MapDescriptor>(JsonConvert.SerializeObject(map))!;
        Assert.That(copy.Fog,Is.EqualTo("normal"));Assert.That(copy.Brightness,Is.EqualTo(93));
        Assert.That(copy.WeatherEffects.RainFog,Is.EqualTo("storm"));Assert.That(copy.WeatherEffects.RainFogOpacity,Is.EqualTo(155));
        Assert.That(copy.WeatherEffects.RainFogXSpeed,Is.EqualTo(-2));Assert.That(copy.WeatherEffects.RainBrightness,Is.EqualTo(60));
    }
    [Test]
    public void ForcedRainPuddlesRecoverAtJoinAndDryAfterShortEpisode()
    {
        var map=Guid.NewGuid();var window=MapWeatherCycle.Window(map,MapWeatherCycle.CycleMilliseconds*5);
        var start=window.Start-120000;var end=start+30000;
        var state=new RainPuddleState();
        Assert.That(state.Update(map,start+15000,true,true,3,start,end),Is.EqualTo(.25f).Within(.001));
        Assert.That(new RainPuddleState().Update(map,end+45000,true,false,3,start,end),Is.EqualTo(.25f).Within(.001));
        Assert.That(state.Update(map,end+90000,true,false,3,start,end),Is.Zero);
    }
    [TestCase("[X] - Emotes",true)]
    [TestCase("[x] - emotes",true)]
    [TestCase("Effects/[X] - Emotes/Chat",true)]
    [TestCase("Effects\\[X] - Emotes",true)]
    [TestCase("[X] - Emotes old",false)]
    [TestCase("Fire",false)]
    [TestCase(null,false)]
    public void OnlyEmoteFolderAnimationsUseThePassAboveLabels(string? folder,bool expected)
        =>Assert.That(Animation.IsEmoteFolder(folder),Is.EqualTo(expected));
}
