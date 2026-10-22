using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class TeleportToFriendPacket:IntersectPacket
{
    [Key(0)]public string FriendName {get;set;}="";
}
