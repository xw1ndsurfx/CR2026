using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;
public partial class Player
{
    [NotMapped,JsonIgnore] private long _nextPremiumStatus,_lastPremiumStatusSent;
    [NotMapped,JsonIgnore] private long _lastPremiumExpiry=long.MinValue;
    internal void UpdatePremiumStatus(long now)
    {
        if(User==null || Client==null || now<_nextPremiumStatus)return;
        _nextPremiumStatus=now+1000;
        var expires=Intersect.Server.LogiCoins.LogiCoinPurchaseRuntime.PremiumUntil(this);
        var utc=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var active=expires>utc?expires:0;
        if(active==_lastPremiumExpiry && now-_lastPremiumStatusSent<30000)return;
        _lastPremiumStatusSent=now;
        _lastPremiumExpiry=active;
        SendPacket(new PremiumStatusPacket{RemainingMilliseconds=Math.Clamp(active-utc,0,3153600000000L)});
    }
}
