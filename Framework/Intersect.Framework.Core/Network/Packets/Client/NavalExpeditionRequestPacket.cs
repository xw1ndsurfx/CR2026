using Intersect.Framework.Core.Naval;
using MessagePack;
namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class NavalExpeditionRequestPacket : IntersectPacket
{
    [Key(0)] public NavalExpeditionAction Action { get; set; }
    [Key(1)] public Guid Id { get; set; }
}
