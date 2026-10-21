using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class NavalManagementPacket : IntersectPacket
{
    [Key(0)] public string Json {get;set;}="{}";
}
