using Newtonsoft.Json;
namespace Intersect.Framework.Core.Naval;

public enum NavalSkillKind : byte { RammingSpeed, FullSail, TakeCover, OpenFire, RepairBonus }
public enum NavalRepairKind : byte { Hammering, Caulking, Pumping }
public enum NavalTaskKind : byte { ShipsSunk, CannonHits, HullDamage, Repairs, Dockings, CargoUnloaded, SailingLevel, GunneryLevel, ShipwrightLevel }
public enum NavalManageAction : byte { Ammo, Deposit, Withdraw, BuyAmmo, SelectAmmo, Skills, BuySkill, UseSkill, Repair, RepairInput, CancelRepair, OfferHelm, AcceptHelm, ReclaimHelm, Dock, RepairMenu }
public sealed class NavalAmmoStack {public Guid ItemId {get;set;} public int Quantity {get;set;}}
public sealed class NavalCareerData
{
    public List<NavalAmmoStack> Ammo {get;set;}=[];
    public HashSet<NavalSkillKind> Skills {get;set;}=[];
    public long RepairExperience {get;set;}
    public Dictionary<NavalTaskKind,long> Counters {get;set;}=[];
    public bool IsValid()=>Ammo is {Count:<=512} && Ammo.All(a=>a!=null && a.ItemId!=Guid.Empty && a.Quantity is >0 and <=1000000000) &&
        Ammo.Select(a=>a.ItemId).Distinct().Count()==Ammo.Count && Skills!=null && Skills.All(Enum.IsDefined) && RepairExperience is >=0 and <=200000000000000L &&
        Counters is {Count:<=32} && Counters.All(p=>Enum.IsDefined(p.Key) && p.Value is >=0 and <=200000000000000L);
}
public sealed class NavalSkillDefinition
{
    public NavalSkillKind Kind {get;set;}
    public int RequiredLevel {get;set;}=1;
    public Guid CurrencyItemId {get;set;}
    public int Price {get;set;}=100;
    public int DurationSeconds {get;set;}=10;
    public int CooldownSeconds {get;set;}=60;
    public int PowerPercent {get;set;}=25;
    public string Icon {get;set;}="";
    public bool IsValid()=>Enum.IsDefined(Kind) && RequiredLevel is >=1 and <=200 && Price is >=0 and <=1000000000 && DurationSeconds is >=1 and <=120 &&
        CooldownSeconds is >=1 and <=3600 && PowerPercent is >=1 and <=200 && NavalPresentation.SafeAsset(Icon);
    public static NavalSkillDefinition[] Defaults()=>Enum.GetValues<NavalSkillKind>().Select((k,i)=>new NavalSkillDefinition {Kind=k,RequiredLevel=1+i*5,Price=100*(i+1)}).ToArray();
    public static string Caption(NavalSkillKind k)=>k switch {NavalSkillKind.RammingSpeed=>"Ramming speed",NavalSkillKind.FullSail=>"Full sail",NavalSkillKind.TakeCover=>"Take cover",NavalSkillKind.OpenFire=>"Open fire",_=>"Repair bonus"};
}
public sealed class NavalRepairSettings
{
    public bool Enabled {get;set;}=true;
    public int HullPerSuccess {get;set;}=5;
    public int ExperiencePerSuccess {get;set;}=20;
    public int RoundSeconds {get;set;}=20;
    public int RequiredHits {get;set;}=5;
    public int CooldownSeconds {get;set;}=10;
    public int MinimumRoundMilliseconds {get;set;}=3000;
    public int MaximumLevel {get;set;}=100;
    public int BaseExperience {get;set;}=100;
    public double ExperienceGrowth {get;set;}=1.12;
    public SailingSettings Curve()=>new(){MaximumLevel=MaximumLevel,BaseExperience=BaseExperience,ExperienceGrowth=ExperienceGrowth};
    public bool IsValid()=>HullPerSuccess is >=1 and <=25 && ExperiencePerSuccess is >=0 and <=100000 && RoundSeconds is >=5 and <=120 && RequiredHits is >=3 and <=30 &&
        CooldownSeconds is >=1 and <=300 && MinimumRoundMilliseconds is >=1000 and <=10000 && MinimumRoundMilliseconds<RoundSeconds*1000 && Curve().IsValid();
}
public sealed class NavalAmmoOffer
{
    public CannonAmmoKind Kind {get;set;}
    public Guid ItemId {get;set;}
    public int Quantity {get;set;}
    public int MainQuantity {get;set;}
    public bool ForSale {get;set;}
    public int Price {get;set;}
    public string Currency {get;set;}="";
    public int SailingLevel {get;set;}
    public int GunneryLevel {get;set;}
    public bool Available {get;set;}
    public string Icon {get;set;}="";
}
public sealed class NavalSkillView
{
    public NavalSkillDefinition Definition {get;set;}=new();
    public bool Owned {get;set;}
    public int CooldownMilliseconds {get;set;}
}
public sealed class NavalManagementState
{
    public NavalManageAction Mode {get;set;}
    public string Message {get;set;}="";
    public bool SkillShop {get;set;}
    public bool Shop {get;set;}
    public NavalAmmoOffer[] Ammo {get;set;}=[];
    public NavalSkillView[] Skills {get;set;}=[];
    public Guid ShipId {get;set;}
    public Guid OwnerId {get;set;}
    public Guid HelmsmanId {get;set;}
    public Guid HelmOfferId {get;set;}
    public string HelmOwnerName {get;set;}="";
    public CannonAmmoKind SelectedAmmo {get;set;}
    public int SailingLevel {get;set;}=1;
    public int GunneryLevel {get;set;}=1;
    public SailingProgress Shipwright {get;set;}
    public Guid RepairToken {get;set;}
    public NavalRepairKind RepairKind {get;set;}
    public int RepairHits {get;set;}
    public int RepairRequiredHits {get;set;}
    public int RepairTarget {get;set;}
    public int RepairRemainingMilliseconds {get;set;}
    public int RepairElapsedMilliseconds {get;set;}
}

public static class NavalRepairRules
{
    public static bool Matches(NavalRepairKind kind,int target,int input,long elapsed)=>Enum.IsDefined(kind) && target is >=0 and <=2 && input==target && elapsed>=0 &&
        (kind!=NavalRepairKind.Hammering || elapsed%1600 is >=500 and <=1150);
    public static bool Complete(NavalRepairSettings settings,int hits,long elapsed)=>settings.Enabled && hits>=settings.RequiredHits &&
        elapsed>=settings.MinimumRoundMilliseconds && elapsed<=settings.RoundSeconds*1000L;
}
