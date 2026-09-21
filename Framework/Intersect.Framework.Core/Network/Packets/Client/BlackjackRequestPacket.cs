using Intersect.Network.Packets.MiniGames;
using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public sealed partial class BlackjackRequestPacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId {get;set;}
    [Key(1)] public Guid ViewId {get;set;}
    [Key(2)] public long RequestId {get;set;}
    [Key(3)] public long HandId {get;set;}
    [Key(4)] public long Revision {get;set;}
    [Key(5)] public BlackjackRequestKind Kind {get;set;}
    [Key(6)] public long Amount {get;set;}
    [IgnoreMember] public bool IsValid=>TableInstanceId!=Guid.Empty && ViewId!=Guid.Empty && RequestId>0 && HandId>=0 && Revision>=0 &&
        Kind is >=BlackjackRequestKind.Refresh and <=BlackjackRequestKind.SelectBack &&
        (Kind==BlackjackRequestKind.Bet ? Amount is >=2 and <=1_000_000_000 && Amount%2==0 :
         Kind==BlackjackRequestKind.SelectBack ? Amount is >=0 and <6 : Amount==0);
}
