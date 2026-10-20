using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Server;
[MessagePackObject(AllowPrivate = true)]
public partial class HarbourStatePacket : IntersectPacket
{
    [Key(0)] public HarbourEntry[] Entries { get; set; } = [];
    [Key(1)] public string Message { get; set; } = "";
    [Key(2)] public bool Premium { get; set; }
    [Key(4)] public HarbourView View { get; set; }
    [Key(3)] public bool Close { get; set; }
}
[MessagePackObject]
public sealed class HarbourEntry
{
    [Key(12)] public int CargoCount {get;set;}
    [Key(11)] public string ShipName {get;set;}="";
    [Key(0)] public string ModelId { get; set; } = "";
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public string Currency { get; set; } = "";
    [Key(3)] public int Price { get; set; }
    [Key(4)] public int RepairPrice { get; set; }
    [Key(5)] public bool PremiumRequired { get; set; }
    [Key(6)] public bool Owned { get; set; }
    [Key(7)] public int Health { get; set; }
    [Key(8)] public long Balance { get; set; }
    [Key(10)] public bool ForSale { get; set; } = true;
    [Key(9)] public bool CurrencyAvailable { get; set; }
}
