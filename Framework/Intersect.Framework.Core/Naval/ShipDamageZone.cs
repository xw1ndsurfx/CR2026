using MessagePack;
namespace Intersect.Framework.Core.Naval;
[MessagePackObject]
public sealed class ShipDamageZone
{
    [Key(0)] public string Id {get;set;}=Guid.NewGuid().ToString("N");
    [Key(1)] public double X {get;set;}=.5;
    [Key(2)] public double Y {get;set;}=-1.4;
    [Key(3)] public int HealthThreshold {get;set;}=100;
    [Key(4)] public Guid DamageAnimationId {get;set;}
    [Key(5)] public bool Repairable {get;set;}=true;
    [Key(6)] public NavalRepairKind RepairKind {get;set;}
    public bool IsValid()=>!string.IsNullOrWhiteSpace(Id) && Id.Length<=40 && double.IsFinite(X) && double.IsFinite(Y) &&
        Math.Abs(X)<=4 && Math.Abs(Y)<=4 && HealthThreshold is >=1 and <=100 && Enum.IsDefined(RepairKind) && (!Repairable || ShipDeck.Contains(new(X,Y)));
    public static ShipDamageZone[] Defaults()=>Enumerable.Range(0,6).Select(i=>new ShipDamageZone
    {Id="hull-"+i,X=i%2==0?.5:-.5,Y=i<2?-1.4:i<4?.7:1.4,HealthThreshold=100-i*16}).ToArray();
    public static IEnumerable<ShipDamageZone> Active(ShipDefinition ship,int health,int maximumHealth)
    {
        if(health<=0 || maximumHealth<=0 || !ShipDeck.Supports(ship.Graphics))return [];
        var zones=ship.DamageZones.Length>0?ship.DamageZones:ship.UseDefaultDamageZones?Defaults():[];
        return zones.Where(zone=>health*100L<maximumHealth*(long)zone.HealthThreshold);
    }
}
