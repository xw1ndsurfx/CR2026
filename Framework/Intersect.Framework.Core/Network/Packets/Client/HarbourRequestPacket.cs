using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;
[MessagePackObject(AllowPrivate = true)]
public partial class HarbourRequestPacket : IntersectPacket
{
    [Key(5)] public Guid ParcelId {get;set;}
    [Key(4)] public Guid ShipId {get;set;}
    [Key(3)] public string Name {get;set;}="";
    [Key(0)] public HarbourAction Action { get; set; }
    [Key(2)] public HarbourView View { get; set; }
    [Key(1)] public string ModelId { get; set; } = "";
}
