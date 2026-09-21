using System;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
namespace Intersect.Server.MiniGames.Blackjack;
public sealed class BlackjackRequestGuard(Guid table,Guid view)
{
    private long _last,_window;private int _count;
    public bool Accept(BlackjackRequestPacket p,long now)
    {
        if(!p.IsValid || p.TableInstanceId!=table || p.ViewId!=view || p.RequestId<=_last)return false;
        if(now-_window>=1000){_window=now;_count=0;}
        if(p.Kind!=BlackjackRequestKind.Leave && _count>=8)return false;
        ++_count;_last=p.RequestId;return true;
    }
}
