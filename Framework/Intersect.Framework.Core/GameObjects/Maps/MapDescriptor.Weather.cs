using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
namespace Intersect.Framework.Core.GameObjects.Maps;
public sealed class MapWeatherEffects
{
    public static MapWeatherEffects NewMapDefaults() => new()
    {
        RandomWeather = true, GeneratedRain = true, Flashes = true, Lightning = true,
        OverrideRainFog = true, RainFog = "xyloop_cloud1_black.png", RainFogOpacity = 150,
        RainFogXSpeed = 3, RainFogYSpeed = -3, OverrideRainBrightness = true, RainBrightness = 70,
        GroundSplashes = true, RainSound = "rain_medium.wav", ThunderSound = "thunder_crack_3.wav",
    };
    public bool OverrideRainFog {get;set;}
    public string RainFog {get;set;} = "";
    public int RainFogOpacity {get;set;} = 100;
    public int RainFogXSpeed {get;set;}
    public int RainFogYSpeed {get;set;}
    public bool OverrideRainBrightness {get;set;}
    public int RainBrightness {get;set;} = 70;
    public bool RandomWeather {get;set;}
    public bool GeneratedRain {get;set;}
    public string RainSound {get;set;} = "";
    public string ThunderSound {get;set;} = "";
    public int PuddleDryMinutes {get;set;} = 3;
    public bool Flashes {get;set;}
    public int FlashOpacity {get;set;}=25;
    public bool Lightning {get;set;}
    public Guid LightningAnimationId {get;set;}
    public bool GroundSplashes {get;set;}
    public Guid SplashAnimationId {get;set;}
}
public partial class MapDescriptor
{
    public override void Load(string json, bool keepCreationTime = false)
    {
        var source=Newtonsoft.Json.Linq.JObject.Parse(json);
        if(source.GetValue(nameof(WeatherEffects),StringComparison.OrdinalIgnoreCase)==null &&
            source.GetValue(nameof(WeatherEffectsJson),StringComparison.OrdinalIgnoreCase)==null)
            WeatherEffects=new();
        base.Load(json,keepCreationTime);
    }
    [NotMapped] public MapWeatherEffects WeatherEffects {get;set;}=MapWeatherEffects.NewMapDefaults();
    [NotMapped,JsonIgnore] public bool HasConfiguredRain => WeatherIntensity > 0 && (WeatherEffects.GeneratedRain || WeatherAnimationId != Guid.Empty);
    public bool WeatherActive(long utc, long forcedRainUntil = 0) => !IsIndoors &&
        (!WeatherEffects.RandomWeather || HasConfiguredRain && forcedRainUntil > utc || MapWeatherCycle.IsActive(Id,utc));
    public bool RainActive(long utc, long forcedRainUntil = 0) => HasConfiguredRain && WeatherActive(utc,forcedRainUntil);
    public int RainAdjustedBrightness(int normal, long utc, long forcedRainUntil = 0) =>
        RainActive(utc,forcedRainUntil) && WeatherEffects.OverrideRainBrightness ? Math.Clamp(WeatherEffects.RainBrightness,0,100) : normal;
    [Column("WeatherEffects"),JsonIgnore]
    public string WeatherEffectsJson
    {
        get=>JsonConvert.SerializeObject(WeatherEffects);
        set=>WeatherEffects=string.IsNullOrWhiteSpace(value)?new():JsonConvert.DeserializeObject<MapWeatherEffects>(value)??new();
    }
}
/// <summary>Global storms use server UTC, so every outdoor map and reconnect shares the same weather.</summary>
public static class MapWeatherCycle
{
    public const long StormMilliseconds=15*60*1000;
    public const long CycleMilliseconds=2*60*60*1000;
    public static (long Start,long End) Window(Guid mapId,long utcMilliseconds)
    {
        var cycle=Math.Max(0,utcMilliseconds)/CycleMilliseconds;
        ulong hash=14695981039346656037UL;
        // The schedule is shared worldwide; map IDs must not shift storm start/end times.
        hash=unchecked((hash^(ulong)cycle)*1099511628211UL);
        hash^=hash>>33;hash=unchecked(hash*0xff51afd7ed558ccdUL);hash^=hash>>33;
        // At least five clear minutes on either side; every episode lasts exactly fifteen minutes.
        var start=cycle*CycleMilliseconds+5*60*1000+(long)(hash%(ulong)(CycleMilliseconds-StormMilliseconds-10*60*1000));
        return(start,start+StormMilliseconds);
    }
    public static bool IsActive(Guid mapId,long utcMilliseconds)
    {
        var window=Window(mapId,utcMilliseconds);return utcMilliseconds>=window.Start && utcMilliseconds<window.End;
    }
}
