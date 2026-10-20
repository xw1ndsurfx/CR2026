using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;

[MessagePackObject(AllowPrivate = true)]
public partial class ShipActionPacket : IntersectPacket
{
    [Key(0)] public Guid ShipId { get; set; }
    [Key(1)] public ShipAction Action { get; set; }
}
