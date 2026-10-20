using MessagePack;
namespace Intersect.Framework.Core.Naval;

public enum NavalCargoKind : byte { Crate, TreasureChest, RoyalChest, Materials, Equipment }
public sealed class NavalLootEntry
{
    public Guid ItemId {get;set;}
    public double ChancePercent {get;set;}=100;
    public int MinimumQuantity {get;set;}=1;
    public int MaximumQuantity {get;set;}=1;
    public int Quality {get;set;}=1;
    public NavalCargoKind Kind {get;set;}
    public bool IsValid()=>ItemId!=Guid.Empty && double.IsFinite(ChancePercent) && ChancePercent is >=0 and <=100 &&
        MinimumQuantity>=1 && MaximumQuantity>=MinimumQuantity && MaximumQuantity<=1000000 && Quality is >=1 and <=5 && Enum.IsDefined(Kind);
}
[MessagePackObject]
public sealed class NavalCargoParcel
{
    [Key(0)] public Guid Id {get;set;}=Guid.NewGuid();
    [Key(1)] public Guid ItemId {get;set;}
    [Key(2)] public int Quantity {get;set;}=1;
    [Key(3)] public int Quality {get;set;}=1;
    [Key(4)] public NavalCargoKind Kind {get;set;}
    public bool IsValid()=>Id!=Guid.Empty && ItemId!=Guid.Empty && Quantity is >=1 and <=1000000 && Quality is >=1 and <=5 && Enum.IsDefined(Kind);
}
public static class NavalCargo
{
    public static bool Add(List<NavalCargoParcel> cargo,NavalCargoParcel parcel,int capacity)
    {
        if(!parcel.IsValid() || capacity<=0 || capacity>200 || cargo.Any(p=>p.Id==parcel.Id)) return false;
        if(cargo.Count>=capacity)
        {
            var worst=cargo.MinBy(p=>p.Quality)!;
            if(worst.Quality>=parcel.Quality) return false;
            cargo.Remove(worst);
        }
        cargo.Add(parcel);return true;
    }
    public static NavalCargoParcel[] Roll(IEnumerable<NavalLootEntry> entries,Func<double> chance,Func<int,int,int> quantity) =>
        entries.Where(e=>e.IsValid() && chance()*100<e.ChancePercent).Select(e=>new NavalCargoParcel {
            ItemId=e.ItemId,Quantity=quantity(e.MinimumQuantity,e.MaximumQuantity+1),Quality=e.Quality,Kind=e.Kind}).ToArray();
    public static string Caption(NavalCargoKind kind)=>kind switch {
        NavalCargoKind.Crate=>"Cargo crate",NavalCargoKind.TreasureChest=>"Treasure chest",NavalCargoKind.RoyalChest=>"Royal chest",
        NavalCargoKind.Materials=>"Ship materials",_=>"Equipment cargo"};
}
