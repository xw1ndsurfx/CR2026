using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Framework.Core.Naval;

public sealed class NavalConfiguration
{
    public int SkillsVersion {get;set;}=1;
    [System.Runtime.Serialization.OnDeserializing]
    private void ReadSkillsVersion(System.Runtime.Serialization.StreamingContext context)=>SkillsVersion=0;
    [System.Runtime.Serialization.OnDeserialized]
    private void UpgradeSkills(System.Runtime.Serialization.StreamingContext context)
    {
        if(SkillsVersion>=1 || Skills==null) return;
        // Append only the new abilities once; preserve authored prices, omissions and future removals.
        Skills=Skills.Concat(NavalSkillDefinition.Defaults().Where(d=>(int)d.Kind>=5 && !Skills.Any(s=>s?.Kind==d.Kind))).ToArray();
        SkillsVersion=1;
    }
    public int CareersVersion {get;set;}=1;
    public bool Enabled { get; set; }
    public double DockInteractionRange { get; set; } = 1.5;
    public int LaunchSearchRadius { get; set; } = 5;
    public double ShopInteractionRange { get; set; } = 4;
    public int ShopSessionSeconds { get; set; } = 120;
    public bool CannonsEnabled { get; set; }
    public int CombatTimeoutSeconds { get; set; } = 12;
    public bool PlayerShipDamage { get; set; }
    public int CannonDamage { get; set; } = 10;
    public int CannonReloadMilliseconds { get; set; } = 4000;
    public double CannonRange { get; set; } = 12;
    public double CannonSpeed { get; set; } = 12;
    public NavalPresentation Presentation {get;set;}=new();
    public GunnerySettings Gunnery {get;set;}=new();
    public CannonAmmunition[] Ammunition {get;set;}=CannonAmmunition.Defaults();
    public NavalRepairSettings Repair {get;set;}=new();
    public NavalSkillDefinition[] Skills {get;set;}=NavalSkillDefinition.Defaults();
    public SailingSettings Sailing {get;set;}=new();
    public HerboniaPatrol[] Herbonia { get; set; } = [];
    public ShipDefinition[] Ships { get; set; } = [];
    [JsonExtensionData] public IDictionary<string, JToken>? AdditionalData { get; set; }
    [JsonIgnore] public string? SourceJson { get; set; }

    public void Validate(int width, int height)
    {
        if (Repair?.IsValid()!=true || Skills==null || Skills.Length>Enum.GetValues<NavalSkillKind>().Length || Skills.Any(s=>s?.IsValid()!=true) || Skills.Select(s=>s.Kind).Distinct().Count()!=Skills.Length || Gunnery?.IsValid()!=true || Ammunition==null || Ammunition.Length is <1 or >10 || Ammunition.Any(a=>a?.IsValid()!=true || (a.Kind==CannonAmmoKind.Standard?a.RequiresItem || a.RequiredLevel!=1:!a.RequiresItem)) ||
            Ammunition.Select(a=>a.Kind).Distinct().Count()!=Ammunition.Length || !Ammunition.Any(a=>a.Kind==CannonAmmoKind.Standard) || CombatTimeoutSeconds is <3 or >120 || Herbonia==null || Herbonia.Length>32 || Herbonia.Any(p=>p?.IsValid()!=true || p.Ship.X>=width || p.Ship.Y>=height || p.BoardingDungeonId!=Guid.Empty && (p.BoardingEntryX>=width || p.BoardingEntryY>=height) || p.Ship.DockX>=width || p.Ship.DockY>=height) ||
            Herbonia.Select(p=>p.Ship.Id).Concat(Ships?.Where(p=>p!=null).Select(p=>p.Id)??[]).GroupBy(id=>id).Any(g=>g.Count()>1) || Presentation?.IsValid()!=true || Sailing?.IsValid()!=true || !double.IsFinite(DockInteractionRange) || DockInteractionRange is < 1 or > 5 ||
            LaunchSearchRadius is < 3 or > 10 || !double.IsFinite(ShopInteractionRange) ||
            ShopInteractionRange is < 1 or > 10 || ShopSessionSeconds is < 10 or > 600 ||
            CannonDamage is < 1 or > 100 || CannonReloadMilliseconds is < 1000 or > 60000 ||
            !double.IsFinite(CannonRange) || CannonRange is < 1 or > 40 ||
            !double.IsFinite(CannonSpeed) || CannonSpeed is < 1 or > 30 || Ships == null || Ships.Length > 64 || Ships.Any(s => s == null || !s.IsValid() ||
                s.X >= width || s.Y >= height || s.DockX >= width || s.DockY >= height) ||
            Ships.Select(s => s.Id).Distinct().Count() != Ships.Length)
            throw new InvalidDataException("Invalid navigation settings. Check ship ids, map positions and movement settings.");
    }
}
