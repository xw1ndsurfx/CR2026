using MessagePack;
namespace Intersect.Network.Packets.RoyalContracts;
public enum RoyalSealShopAction { Select, Buy, Close }
[MessagePackObject]
public partial class RoyalSealShopRequestPacket : IntersectPacket
{
    [Key(0)] public RoyalSealShopAction Action { get; set; }
    [Key(1)] public Guid ShopId { get; set; }
    [Key(2)] public int Slot { get; set; }
    [Key(3)] public int Quantity { get; set; } = 1;
    [Key(4)] public int ExpectedPrice { get; set; }
    [Key(5)] public Guid ExpectedItemId { get; set; }
}
[MessagePackObject]
public partial class RoyalSealShopStatePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public bool Close { get; set; }
}
