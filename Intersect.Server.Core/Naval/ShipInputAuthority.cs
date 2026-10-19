using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;

namespace Intersect.Server.Naval;

internal sealed class ShipInputAuthority
{
    internal long LastInput { get; private set; }
    internal ShipInput Input { get; private set; } = new(0, 0, true);
    internal void Reset() { LastInput = 0; Input = new(0, 0, true); }
    internal bool Accept(ShipState ship, Guid actor, bool alive, ShipInputPacket packet, long now)
    {
        if (!alive || actor == Guid.Empty || ship.CaptainId != actor || packet.ShipId != ship.Id ||
            packet.Thrust is < -1 or > 1 || packet.Turn is < -1 or > 1 || now - LastInput < 25) return false;
        LastInput = now; Input = new(packet.Thrust, packet.Turn, packet.Anchor);
        return true;
    }
    internal ShipInput ForTick(long now, bool busy) => busy || now - LastInput > 500 ? new(0, 0, true) : Input;
}
