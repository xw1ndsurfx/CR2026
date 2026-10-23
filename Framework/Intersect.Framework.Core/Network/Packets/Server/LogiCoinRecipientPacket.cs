using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class LogiCoinRecipientPacket:IntersectPacket
{
    [Key(0)] public Guid UserId {get;set;}
    [Key(1)] public string Account {get;set;}="";
    [Key(2)] public LogiCoinRecipientCharacter[] Characters {get;set;}=[];
}
[MessagePackObject]
public sealed class LogiCoinRecipientCharacter
{
    [Key(0)] public Guid Id {get;set;}
    [Key(1)] public string Name {get;set;}="";
}
[MessagePackObject]
public sealed class LogiCoinPurchaseHistoryRow
{
    [Key(0)] public string Offer {get;set;}="";
    [Key(1)] public string Recipient {get;set;}="";
    [Key(2)] public int Price {get;set;}
    [Key(3)] public bool Completed {get;set;}
    [Key(4)] public long CreatedUtc {get;set;}
}
