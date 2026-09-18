using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PokerRequestPacket packet) => PokerRuntime.Handle(client, packet);
}
