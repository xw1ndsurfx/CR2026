using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.MiniGames;

internal sealed class PotionClientModel
{
    public PotionStatePacket? Current { get; private set; }
    public string ErrorCode { get; private set; } = string.Empty;
    public bool Pending => _pending > 0;

    private Guid _dismissed;
    private long _sequence;
    private long _request;
    private long _pending;
    private long _sent;
    private long _received;

    public bool Apply(PotionStatePacket packet, Guid playerId, long now)
    {
        if (!packet.IsValid || packet.PlayerId != playerId) return false;
        if (packet.Sequence <= _sequence) return false;
        _sequence = packet.Sequence;

        if (packet.SessionId == _dismissed) return false;
        if (_pending > 0 && packet.RequestId >= _pending)
        {
            _pending = 0;
            ErrorCode = packet.ErrorCode;
        }

        if (packet.Closed)
        {
            if (Current?.SessionId != packet.SessionId) return false;
            Current = null;
            _pending = 0;
            ErrorCode = packet.ErrorCode;
            return true;
        }

        if (Current?.SessionId != packet.SessionId)
        {
            _pending = 0;
            ErrorCode = string.Empty;
        }

        Current = packet;
        _received = now;
        if (packet.RequestId > 0 || packet.ErrorCode.Length > 0) ErrorCode = packet.ErrorCode;
        return true;
    }

    public PotionRequestPacket? Request(PotionRequestKind kind, long now, int column = 0)
    {
        if (Current?.State is not { } state) return null;
        if (Pending && kind != PotionRequestKind.Leave &&
            !(kind == PotionRequestKind.Refresh && now - _sent >= 5_000))
            return null;

        var packet = new PotionRequestPacket
        {
            SessionId = Current.SessionId,
            RequestId = _request + 1,
            Revision = state.Revision,
            Kind = kind,
            Column = column,
        };

        if (!packet.IsValid) return null;
        _request = packet.RequestId;
        _pending = packet.RequestId;
        _sent = now;
        ErrorCode = string.Empty;
        return packet;
    }

    public bool NeedsRefresh(long now) =>
        Current != null && (Pending ? now - _sent >= 5_000 : now - _received >= 10_000);

    public void Dismiss()
    {
        _dismissed = Current?.SessionId ?? Guid.Empty;
        Current = null;
        _pending = 0;
    }
}
