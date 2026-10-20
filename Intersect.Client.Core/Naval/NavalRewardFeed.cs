using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Naval;

/// <summary>Bounded, session-local presentation queue. Receipts never modify gameplay state.</summary>
internal sealed class NavalRewardFeed
{
    private readonly HashSet<Guid> _seen=[];
    private readonly Queue<Guid> _seenOrder=[];
    private readonly Queue<(NavalRewardReceipt Receipt,long Received)> _pending=[];
    private NavalRewardReceipt? _current;
    private long _until;
    internal void Clear() {_seen.Clear();_seenOrder.Clear();_pending.Clear();_current=null;_until=0;}
    internal void Observe(IEnumerable<NavalRewardReceipt> receipts,bool baseline,long now)
    {
        if(baseline) Clear();
        foreach(var receipt in receipts.Take(16))
        {
            if(receipt==null || receipt.Id==Guid.Empty || !_seen.Add(receipt.Id)) continue;
            _seenOrder.Enqueue(receipt.Id);
            while(_seenOrder.Count>256) _seen.Remove(_seenOrder.Dequeue());
            // On login, establish a baseline instead of replaying old rewards from the server history.
            if(baseline || receipt.AgeMilliseconds is <0 or >6000 || receipt.SailingExperience<0 ||
                receipt.Cargo==null || receipt.Cargo.Length>200 || receipt.Cargo.Any(c=>c?.IsValid()!=true)) continue;
            if(_pending.Count>=8) _pending.Dequeue();
            _pending.Enqueue((receipt,now));
        }
    }
    internal NavalRewardReceipt? Current(long now)
    {
        if(_current!=null && now<_until) return _current;
        _current=null;
        while(_pending.TryDequeue(out var next))
        {
            if(now-next.Received>10000) continue;
            _current=next.Receipt;_until=now+6000;break;
        }
        return _current;
    }
}
