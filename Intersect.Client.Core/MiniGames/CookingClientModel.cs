using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.MiniGames;

internal sealed class CookingClientModel
{
    public CookingStatePacket? Current { get; private set; }
    public string ErrorCode { get; private set; } = string.Empty;
    public bool Pending => _pending > 0;

    private Guid _dismissed;
    private long _sequence;
    private long _request;
    private long _pending;
    private long _sent;
    private long _received;

    public bool Apply(CookingStatePacket packet, Guid playerId, long now)
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
        if (packet.RequestId > 0 || packet.ErrorCode.Length > 0)
            ErrorCode = packet.ErrorCode;

        return true;
    }

    public CookingRequestPacket? Request(
        CookingRequestKind kind,
        long now,
        Guid recipeId = default,
        Guid partnerId = default,
        bool accept = false
    )
    {
        if (Current?.State is not { } state) return null;

        if (Pending &&
            kind != CookingRequestKind.Leave &&
            !(kind == CookingRequestKind.Refresh && now - _sent >= 5_000))
        {
            return null;
        }

        var packet = new CookingRequestPacket
        {
            SessionId = Current.SessionId,
            RequestId = _request + 1,
            Revision = state.Revision,
            Kind = kind,
            RecipeId = recipeId,
            PartnerId = partnerId,
            Accept = accept,
        };

        if (!packet.IsValid) return null;

        _request = packet.RequestId;
        _pending = packet.RequestId;
        _sent = now;
        ErrorCode = string.Empty;
        return packet;
    }

    public bool NeedsRefresh(long now) =>
        Current != null &&
        (Pending ? now - _sent >= 5_000 : now - _received >= 10_000);

    public void Dismiss()
    {
        _dismissed = Current?.SessionId ?? Guid.Empty;
        Current = null;
        _pending = 0;
    }
}
