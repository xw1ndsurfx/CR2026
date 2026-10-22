using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public partial class LogiCoinRecipientRequestPacket:IntersectPacket
{
    [Key(0)]public string Account {get;set;}="";
}
