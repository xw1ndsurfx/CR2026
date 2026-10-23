using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class ServiceShopPurchasePacket:IntersectPacket
{
    [Key(0)] public Guid OfferId {get;set;}
    [Key(1)] public bool Close {get;set;}
}
