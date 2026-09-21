using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.MiniGames;

internal sealed class BlackjackClientModel
{
    public BlackjackStatePacket? Current {get;private set;}
    public string ErrorCode {get;private set;}="";
    public bool Pending=>_pending>0;
    public PokerVictoryTracker Victories {get;}=new();
    private Guid _dismissed,_dealTable;
    private long _sequence,_request,_pending,_sent,_received,_dealHand;
    private int _dealCount;
    public bool Apply(BlackjackStatePacket p,Guid player,long now)
    {
        if(!p.IsValid || p.PlayerId!=player)return false;
        if(Current?.ViewId==p.ViewId && _pending>0 && p.RequestId>=_pending){_pending=0;ErrorCode=p.ErrorCode;}
        if(p.Sequence<=_sequence)return false;
        _sequence=p.Sequence;if(p.ViewId==_dismissed)return false;
        if(p.Closed)
        {if(Current?.ViewId!=p.ViewId)return false;Current=null;_pending=0;ErrorCode=p.ErrorCode;return true;}
        if(Current?.ViewId!=p.ViewId){_pending=0;ErrorCode="";}
        Current=p;_received=now;
        if(p.RequestId>0 || p.ErrorCode.Length>0)ErrorCode=p.ErrorCode;
        return true;
    }
    public BlackjackRequestPacket? Request(BlackjackRequestKind kind,long now,long amount=0)
    {
        if(Current?.State is not {} state)return null;
        if(Pending && kind!=BlackjackRequestKind.Leave && !(kind==BlackjackRequestKind.Refresh && now-_sent>=5000))return null;
        var p=new BlackjackRequestPacket{TableInstanceId=Current.TableInstanceId,ViewId=Current.ViewId,RequestId=_request+1,
            HandId=state.HandId,Revision=state.Revision,Kind=kind,Amount=amount};
        if(!p.IsValid)return null;
        _request=p.RequestId;_pending=p.RequestId;_sent=now;ErrorCode="";return p;
    }
    public bool NeedsRefresh(long now)=>Current!=null && (Pending?now-_sent>=5000:now-_received>=10000);
    public long Seconds(long now)=>Current?.State is {DeadlineUnixMs:>0} s?
        Math.Max(0,(s.DeadlineUnixMs-Current.ServerUnixMs-Math.Max(0,now-_received)+999)/1000):0;
    public bool ObserveDeal()
    {
        if(Current?.State is not {} s)return false;
        var count=s.DealerCards.Length+(s.DealerHoleHidden?1:0)+s.Seats.Sum(p=>p.Hands.Sum(h=>h.Cards.Length));
        if(_dealTable!=Current.TableInstanceId){_dealTable=Current.TableInstanceId;_dealHand=s.HandId;_dealCount=count;return false;}
        if(_dealHand!=s.HandId){_dealHand=s.HandId;_dealCount=0;}
        var changed=count>_dealCount && s.Stage is >=BlackjackStage.Players and <=BlackjackStage.Finished;
        _dealCount=count;return changed;
    }
    public void Dismiss(){_dismissed=Current?.ViewId??Guid.Empty;Current=null;_pending=0;}
}
