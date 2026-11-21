namespace Intersect.Framework.Core.Naval;

public enum HarbourView : byte { Fleet, Shop, Crew }

public enum HarbourAction : byte { Open, Buy, Launch, Repair, Rename, ClaimCargo, JoinCrew, LeaveCrew, ToggleCrewBoarding, DiscardCargo, Sell, ClaimAllCargo }

public sealed class OwnedShip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name {get;set;}="";
    public List<NavalCargoParcel> Cargo {get;set;}=[];
    public string ModelId { get; set; } = "";
    public Guid SourceShopId {get;set;}
    public HarbourOffer? ModelSnapshot {get;set;}
    public int CargoCapacity {get;set;}=20;
    public int Health { get; set; } = 100;
    public int SinkingSerial {get;set;}
    public bool AtSea { get; set; }
    public Guid ReturnMapId { get; set; }
    public int? ReturnX { get; set; }
    public int? ReturnY { get; set; }
    public int ReturnDockX { get; set; }
    public int ReturnDockY { get; set; }
    public bool Sunk => Health == 0;
}

public static class ShipFleetRules
{
    public static OwnedShip FromOffer(HarbourOffer offer,Guid sourceShopId=default)=>new()
    {
        ModelId=offer.Id,CargoCapacity=offer.Ship.CargoCapacity,SourceShopId=sourceShopId,
        ModelSnapshot=Newtonsoft.Json.JsonConvert.DeserializeObject<HarbourOffer>(Newtonsoft.Json.JsonConvert.SerializeObject(offer)),
    };
    public static HarbourOffer? ResolveOffer(OwnedShip owned,HarbourCatalog global,IEnumerable<(Guid Id,HarbourCatalog Catalogue)> shops)
    {
        HarbourOffer? source=null;
        if(owned.SourceShopId!=Guid.Empty)source=shops.Where(s=>s.Id==owned.SourceShopId).SelectMany(s=>s.Catalogue.Offers??[]).FirstOrDefault(o=>o.Id==owned.ModelId);
        else source=global.Offers.FirstOrDefault(o=>o.Id==owned.ModelId);
        if(source?.IsValid()==true)return source;
        if(owned.ModelSnapshot is {} snapshot && snapshot.Id==owned.ModelId && snapshot.IsValid())return snapshot;
        source=global.Offers.FirstOrDefault(o=>o.Id==owned.ModelId);if(source?.IsValid()==true)return source;
        var legacy=shops.SelectMany(s=>s.Catalogue.Offers??[]).Where(o=>o.Id==owned.ModelId && o.IsValid()).Take(2).ToArray();
        return legacy.Length==1?legacy[0]:null;
    }
    public const int MaximumShips = 32;
    // Round up the missing hull fraction; an afloat vessel always costs less than a total loss.
    public static int RepairCost(int fullPrice,int health)
    {
        fullPrice=Math.Max(0,fullPrice);health=Math.Clamp(health,0,100);
        if(health==0) return fullPrice;
        if(health==100 || fullPrice==0) return 0;
        return (int)Math.Min(fullPrice-1,((long)fullPrice*(100-health)+99)/100);
    }
    public static bool CanBuy(IEnumerable<OwnedShip> fleet, string model, bool requiresPremium, bool premium) =>
        (!requiresPremium || premium) && fleet.Count() < MaximumShips && !fleet.Any(s => s.ModelId == model);
    public static int AfterDamage(int health, int damage) => Math.Max(0, Math.Clamp(health, 0, 100) - Math.Max(0, damage));
}

public static class HarbourAccessRules
{
    public static bool Allows(HarbourView view, HarbourAction action) => view switch
    {
        HarbourView.Fleet => action is HarbourAction.Open or HarbourAction.Launch or HarbourAction.Rename or HarbourAction.ClaimCargo or HarbourAction.DiscardCargo or HarbourAction.Repair,
        HarbourView.Crew => action is HarbourAction.Open or HarbourAction.JoinCrew or HarbourAction.LeaveCrew or HarbourAction.ToggleCrewBoarding or HarbourAction.ClaimCargo or HarbourAction.DiscardCargo,
        HarbourView.Shop => action is HarbourAction.Open or HarbourAction.Buy or HarbourAction.Repair or HarbourAction.Sell,
        _ => false,
    };
    public static bool ShopSessionValid(Guid map, double x, double y, Guid sessionMap, double anchorX,
        double anchorY, long now, long expires, double range) => map != Guid.Empty && map == sessionMap &&
        expires > now && double.IsFinite(range) && Math.Pow(x - anchorX, 2) + Math.Pow(y - anchorY, 2) <= range * range;
}
