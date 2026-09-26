using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Roulette;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RouletteRequestPacket packet) =>
        RouletteRuntime.Handle(client, packet);
}
