using Intersect.Framework.Core.MiniGames.Roulette;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.MiniGames;

internal sealed class RouletteClientModel
{
    public RouletteStatePacket? Current { get; private set; }
    public string ErrorCode { get; private set; } = string.Empty;
    public bool Pending => _pendingRequest > 0;

    private Guid _dismissedView;
    private long _sequence;
    private long _request;
    private long _pendingRequest;
    private long _sentAt;
    private long _receivedAt;

    public bool Apply(RouletteStatePacket packet, Guid playerId, long now)
    {
        if (!packet.IsValid || packet.PlayerId != playerId)
            return false;

        if (Current?.ViewId == packet.ViewId &&
            _pendingRequest > 0 &&
            packet.RequestId >= _pendingRequest)
        {
            _pendingRequest = 0;
            ErrorCode = packet.ErrorCode;
        }

        if (packet.Sequence <= _sequence)
            return false;

        _sequence = packet.Sequence;

        if (packet.ViewId == _dismissedView)
            return false;

        if (packet.Closed)
        {
            if (Current?.ViewId != packet.ViewId)
                return false;

            Current = null;
            _pendingRequest = 0;
            ErrorCode = packet.ErrorCode;
            return true;
        }

        if (Current?.ViewId != packet.ViewId)
        {
            _pendingRequest = 0;
            ErrorCode = string.Empty;
        }

        Current = packet;
        _receivedAt = now;

        if (packet.RequestId > 0 || packet.ErrorCode.Length > 0)
            ErrorCode = packet.ErrorCode;

        return true;
    }

    public RouletteRequestPacket? Request(
        RouletteRequestKind kind,
        long now,
        long amount = 0,
        RouletteBetType betType = RouletteBetType.Straight,
        int number = -1
    )
    {
        if (Current?.State is not { } state)
            return null;

        if (Pending &&
            kind != RouletteRequestKind.Leave &&
            !(kind == RouletteRequestKind.Refresh && now - _sentAt >= 5_000))
        {
            return null;
        }

        var packet = new RouletteRequestPacket
        {
            TableInstanceId = Current.TableInstanceId,
            ViewId = Current.ViewId,
            RequestId = _request + 1,
            Revision = state.Revision,
            Kind = kind,
            Amount = kind == RouletteRequestKind.Spin ? amount : 0,
            BetType = betType,
            Number = number,
        };

        if (!packet.IsValid)
            return null;

        _request = packet.RequestId;
        _pendingRequest = packet.RequestId;
        _sentAt = now;
        ErrorCode = string.Empty;
        return packet;
    }

    public bool NeedsRefresh(long now) =>
        Current != null &&
        (Pending ? now - _sentAt >= 5_000 : now - _receivedAt >= 10_000);

    public void Dismiss()
    {
        _dismissedView = Current?.ViewId ?? Guid.Empty;
        Current = null;
        _pendingRequest = 0;
    }
}
