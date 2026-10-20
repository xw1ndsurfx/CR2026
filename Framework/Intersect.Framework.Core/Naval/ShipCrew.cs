using MessagePack;
namespace Intersect.Framework.Core.Naval;

public sealed class ShipCrewPassage
{
    public Guid CaptainId {get;set;}
    public string ShipName {get;set;}="";
    public int Capacity {get;set;}=20;
    public int CaptainSinkingSerial {get;set;}
    public OwnedShip Ship {get;set;}=new();
}
[MessagePackObject]
public sealed class ShipCrewMember
{
    [Key(0)] public Guid PlayerId {get;set;}
    [Key(1)] public string Name {get;set;}="";
    [Key(2)] public bool Captain {get;set;}
}
[MessagePackObject]
public sealed class ShipCrewOffer
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public string Name {get;set;}="";
    [Key(2)] public string CaptainName {get;set;}="";
    [Key(3)] public int Occupied {get;set;}
    [Key(4)] public int Capacity {get;set;}
}
[MessagePackObject]
public sealed class ShipCrewState
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public Guid CaptainId {get;set;}
    [Key(2)] public string ShipName {get;set;}="";
    [Key(3)] public bool BoardingOpen {get;set;}
    [Key(4)] public int Capacity {get;set;}
    [Key(5)] public ShipCrewMember[] Members {get;set;}=[];
    [Key(6)] public ShipCrewOffer[] Offers {get;set;}=[];
    [Key(7)] public int PersonalCargoCount {get;set;}
    [Key(8)] public bool AtDock {get;set;}
}
public static class ShipCrewRules
{
    public static bool CanBoard(bool atDock,bool sameParty,bool boardingOpen,bool stopped,bool alive,int occupied,int capacity)=>
        atDock && sameParty && boardingOpen && stopped && alive && capacity is >=1 and <=8 && occupied>=1 && occupied<capacity;
}
