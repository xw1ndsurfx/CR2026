using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public sealed partial class NavalBoardingOfferPacket : IntersectPacket
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public Guid CaptainId {get;set;}
    [Key(2)] public string CaptainName {get;set;}="";
    [Key(3)] public string ShipName {get;set;}="";
    [Key(4)] public int LifetimeSeconds {get;set;}=20;
}
