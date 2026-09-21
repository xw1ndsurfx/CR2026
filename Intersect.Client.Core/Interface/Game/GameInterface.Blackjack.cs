using Intersect.Client.General;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ClientNetwork=Intersect.Client.Networking.Network;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private readonly object _blackjackInboxLock=new();
    private readonly Queue<BlackjackStatePacket> _blackjackInbox=new();
    private readonly BlackjackClientModel _blackjackModel=new();
    private BlackjackWindow? _blackjackWindow;
    private bool _blackjackDisposed;
    internal void QueueBlackjackState(BlackjackStatePacket p)
    {
        lock(_blackjackInboxLock){if(_blackjackDisposed)return;while(_blackjackInbox.Count>=64)_blackjackInbox.Dequeue();_blackjackInbox.Enqueue(p);}
    }
    private void UpdateBlackjack()
    {
        BlackjackStatePacket[] packets;lock(_blackjackInboxLock){packets=_blackjackInbox.ToArray();_blackjackInbox.Clear();}
        var now=Environment.TickCount64;
        foreach(var p in packets)_blackjackModel.Apply(p,Globals.Me?.Id??Guid.Empty,now);
        if(_blackjackModel.Current==null){_blackjackWindow?.Destroy();_blackjackWindow=null;return;}
        _blackjackWindow??=new BlackjackWindow(GameCanvas,SendBlackjack);
        if(_blackjackWindow.ExitRequested){CloseBlackjackWindow();return;}
        if(_blackjackModel.NeedsRefresh(now))SendBlackjack(BlackjackRequestKind.Refresh,0);
        _blackjackWindow.Update(_blackjackModel);
    }
    private void SendBlackjack(BlackjackRequestKind kind,long amount)
    {var p=_blackjackModel.Request(kind,Environment.TickCount64,amount);if(p!=null)ClientNetwork.SendPacket(p);}
    private bool CloseBlackjackWindow()
    {
        if(_blackjackModel.Current==null && _blackjackWindow==null)return false;
        SendBlackjack(BlackjackRequestKind.Leave,0);_blackjackModel.Dismiss();_blackjackWindow?.Destroy();_blackjackWindow=null;return true;
    }
    private void DisposeBlackjack()
    {
        lock(_blackjackInboxLock){_blackjackDisposed=true;_blackjackInbox.Clear();}
        _blackjackModel.Dismiss();_blackjackWindow?.Destroy();_blackjackWindow=null;
    }
}
