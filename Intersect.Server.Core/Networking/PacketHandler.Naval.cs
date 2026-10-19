using Intersect.Network.Packets.Client;
using Intersect.Utilities;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Editor;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PrepareNavalMapPacket packet)
    {
        if (client.IsEditor) client.Send(Naval.NavalSystem.PrepareMap(packet.MapId));
    }
    public void HandlePacket(Client client, ShipInputPacket packet)
    {
        if (client.Entity is { InGame: true } player)
            Naval.NavalSystem.Input(player, packet, Timing.Global.Milliseconds);
    }
}
