using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.MiniGames;

internal sealed class LockpickingClientModel
{
    private long _lastSequence;
    private long _requestId;
    private long _receivedAt;

    public LockpickingStatePacket? Current { get; private set; }
    public bool Pending { get; private set; }

    public void Apply(LockpickingStatePacket packet, long now)
    {
        if (!packet.IsValid)
            return;

        if (Current != null && packet.SessionId != Current.SessionId && !Current.Closed)
            return;

        if (packet.Sequence <= _lastSequence && Current?.SessionId == packet.SessionId)
            return;

        _lastSequence = packet.Sequence;
        _receivedAt = now;
        Pending = false;

        if (packet.Closed)
        {
            Current = null;
            return;
        }

        Current = packet;
    }

    public LockpickingRequestPacket? Request(LockpickingRequestKind kind, int angle)
    {
        if (Current == null || Pending)
            return null;

        var packet = new LockpickingRequestPacket
        {
            SessionId = Current.SessionId,
            RequestId = ++_requestId,
            Kind = kind,
            Angle = kind == LockpickingRequestKind.Attempt ? Math.Clamp(angle, -90, 90) : 0,
        };

        if (!packet.IsValid)
            return null;

        Pending = kind == LockpickingRequestKind.Attempt;
        return packet;
    }

    public int RemainingMilliseconds(long now)
    {
        if (Current == null)
            return 0;

        return (int)Math.Max(0, Current.RemainingMilliseconds - Math.Max(0, now - _receivedAt));
    }

    public void Dismiss()
    {
        Current = null;
        Pending = false;
    }
}
