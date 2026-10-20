using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;
[MessagePackObject(AllowPrivate = true)]
public partial class HarbourRequestPacket : IntersectPacket
{
    [Key(0)] public HarbourAction Action { get; set; }
    [Key(1)] public string ModelId { get; set; } = "";
}
