using Intersect.Network.Packets.Client;
using Intersect.Utilities;
using Intersect.Framework.Core;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, ShipInputPacket packet)
    {
        if (client.Entity is { InGame: true } player)
            Naval.NavalSystem.Input(player, packet, Timing.Global.Milliseconds);
    }
}
