using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
namespace Intersect.Framework.Core.GameObjects.Maps;
public sealed class MapWeatherEffects
{
    public bool RandomWeather {get;set;}
    public bool GeneratedRain {get;set;}
    public bool Flashes {get;set;}
    public int FlashOpacity {get;set;}=25;
    public bool Lightning {get;set;}
    public Guid LightningAnimationId {get;set;}
    public bool GroundSplashes {get;set;}
    public Guid SplashAnimationId {get;set;}
}
public partial class MapDescriptor
{
    [NotMapped] public MapWeatherEffects WeatherEffects {get;set;}=new();
    [Column("WeatherEffects"),JsonIgnore]
    public string WeatherEffectsJson
    {
        get=>JsonConvert.SerializeObject(WeatherEffects);
        set=>WeatherEffects=string.IsNullOrWhiteSpace(value)?new():JsonConvert.DeserializeObject<MapWeatherEffects>(value)??new();
    }
}
/// <summary>Map-seeded storms use server UTC, so reconnects and nearby clients share the same weather.</summary>
public static class MapWeatherCycle
{
    public const long StormMilliseconds=15*60*1000;
    public const long CycleMilliseconds=2*60*60*1000;
    public static (long Start,long End) Window(Guid mapId,long utcMilliseconds)
    {
        var cycle=Math.Max(0,utcMilliseconds)/CycleMilliseconds;
        ulong hash=14695981039346656037UL;
        foreach(var b in mapId.ToByteArray())hash=unchecked((hash^b)*1099511628211UL);
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
