using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class PremiumConvenienceStatePacket : IntersectPacket
{
    [Key(0)] public string Json {get;set;}="";
    [Key(1)] public bool OpenWindow {get;set;}
    [Key(2)] public string Message {get;set;}="";
}
