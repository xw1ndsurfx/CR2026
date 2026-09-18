using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.MiniGames;

/// <summary>UI-thread state only. Winners, XP and unlock permissions are server-owned.</summary>
internal sealed class PokerClientModel
{
    public PokerStatePacket? Current { get; private set; }
    public long LastSequence { get; private set; }
    public bool Pending => _pendingRequest > 0;
    public string ErrorCode { get; private set; } = string.Empty;
    public PokerVictoryTracker Victories { get; } = new();
    private Guid _dismissedView;
    private long _nextRequest;
    private long _pendingRequest;
    private long _sentAt;
    private long _receivedAt;

    public bool Apply(PokerStatePacket packet, Guid playerId, long monotonicMs)
    {
        if (!packet.IsValid || packet.PlayerId != playerId) return false;
        if (Current?.ViewId == packet.ViewId && _pendingRequest > 0 && packet.RequestId >= _pendingRequest)
        { _pendingRequest = 0; ErrorCode = packet.ErrorCode; }
        if (packet.Sequence <= LastSequence) return false;
        LastSequence = packet.Sequence;
        if (packet.ViewId == _dismissedView) return false;
        if (packet.Closed)
        {
            if (Current?.ViewId != packet.ViewId) return false;
            Current = null; _pendingRequest = 0; ErrorCode = packet.ErrorCode;
            return true;
        }
        if (Current?.ViewId != packet.ViewId) { _pendingRequest = 0; ErrorCode = string.Empty; }
        Current = packet;
        _receivedAt = monotonicMs;
        if (packet.RequestId > 0 || !string.IsNullOrEmpty(packet.ErrorCode)) ErrorCode = packet.ErrorCode;
        return true;
    }

    public PokerRequestPacket? Request(PokerRequestKind kind, long monotonicMs, long amount = 0)
    {
        if (Current?.State is not { } state) return null;
        if (kind == PokerRequestKind.SelectCardBack && (amount < 0 || amount >= MiniGameProgression.BackCount)) return null;
        if (Pending && kind != PokerRequestKind.Leave &&
            !(kind == PokerRequestKind.Refresh && monotonicMs - _sentAt >= 5000)) return null;
        var request = new PokerRequestPacket
        {
            TableInstanceId = Current.TableInstanceId, ViewId = Current.ViewId,
            RequestId = ++_nextRequest, HandId = state.HandId, Revision = state.Revision,
            Kind = kind, RaiseTo = kind == PokerRequestKind.SelectCardBack ? 0 : amount,
            CardBackId = kind == PokerRequestKind.SelectCardBack ? (int)amount : 0,
        };
        _pendingRequest = request.RequestId; _sentAt = monotonicMs; ErrorCode = string.Empty;
        return request;
    }
    public bool NeedsRefresh(long monotonicMs) => Current != null &&
        (Pending ? monotonicMs - _sentAt >= 5000 : monotonicMs - _receivedAt >= 10000);
    public long SecondsRemaining(long monotonicMs)
    {
        if (Current?.State is not { DeadlineUnixMs: > 0 } state) return 0;
        var estimatedServerTime = Current.ServerUnixMs + Math.Max(0, monotonicMs - _receivedAt);
        return Math.Max(0, (state.DeadlineUnixMs - estimatedServerTime + 999) / 1000);
    }
    public void Dismiss() { _dismissedView = Current?.ViewId ?? Guid.Empty; Current = null; _pendingRequest = 0; }
}
