using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace Intersect.Framework.Core.Naval;

public sealed class SailingSettings
{
    public int MaximumLevel {get;set;}=100;
    public int BaseExperience {get;set;}=100;
    public double ExperienceGrowth {get;set;}=1.12;
    public bool IsValid()=>MaximumLevel is >=1 and <=200 && BaseExperience is >=1 and <=1000000000 &&
        double.IsFinite(ExperienceGrowth) && ExperienceGrowth is >=1 and <=2;
    public long Required(int level)=>(long)Math.Clamp(Math.Round(BaseExperience*Math.Pow(ExperienceGrowth,level-1)),1,1000000000000d);
    public SailingProgress Progress(long total)
    {
        var remaining=Math.Clamp(total,0,200000000000000L);var level=1;
        while(level<MaximumLevel && remaining>=Required(level)) {remaining-=Required(level);level++;}
        return new(level,level==MaximumLevel?0:remaining,level==MaximumLevel?0:Required(level));
    }
    public long Cap {get {long total=0;for(var i=1;i<MaximumLevel;i++) total+=Required(i);return total;}}
}
public readonly record struct SailingProgress(int Level,long Experience,long Required);

public sealed class NavalPlayerData
{
    public NavalCareerData Career {get;set;}=new();
    public int Version {get;set;}=2;
    public List<OwnedShip> Ships {get;set;}=[];
    public long GunneryExperience {get;set;}
    public long SailingExperience {get;set;}
    public ShipCrewPassage? CrewPassage {get;set;}
    public long PendingCharacterExperience {get;set;}
    [JsonExtensionData] public IDictionary<string,JToken>? AdditionalData {get;set;}
    public static NavalPlayerData Parse(string? json)
    {
        if(string.IsNullOrWhiteSpace(json)) return new();
        var token=JToken.Parse(json);
        var data=token is JArray ? new NavalPlayerData {Ships=token.ToObject<List<OwnedShip>>()!} : token.ToObject<NavalPlayerData>()!;
        if(data==null || data.Career?.IsValid()!=true || data.Version!=2 || data.Ships==null || data.Ships.Count>32 || data.Ships.Any(s=>s==null || s.CargoCapacity is <0 or >200 || s.Cargo==null || s.Cargo.Count>200 || s.Cargo.Any(p=>p?.IsValid()!=true) || s.Cargo.Select(p=>p.Id).Distinct().Count()!=s.Cargo.Count) || data.GunneryExperience is <0 or >200000000000000L || data.SailingExperience is <0 or >200000000000000L || data.PendingCharacterExperience is <0 or >200000000000000L ||
            data.CrewPassage is {} passage && (passage.CaptainSinkingSerial<0 || passage.Capacity is <0 or >200 || passage.Ship==null || passage.Ship.Id==Guid.Empty || passage.Ship.CargoCapacity is <0 or >200 || passage.Ship.Cargo==null || passage.Ship.Cargo.Count>200 || passage.Ship.Cargo.Any(p=>p?.IsValid()!=true) || passage.Ship.Cargo.Select(p=>p.Id).Distinct().Count()!=passage.Ship.Cargo.Count))
            throw new JsonException("Invalid naval player data");
        return data;
    }
}
public static class ShipNames
{
    public static string? Normalize(string? name)
    {
        if(name==null || name.Length>128) return null;
        try {name=Regex.Replace(name.Normalize(NormalizationForm.FormC).Trim()," +"," ");}
        catch(ArgumentException) {return null;}
        return name.Length is >=1 and <=32 && Regex.IsMatch(name,@"^[\p{L}\p{M}\p{N} .'’-]+$")?name:null;
    }
}
