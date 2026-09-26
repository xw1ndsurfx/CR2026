using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Cooking;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, CookingRequestPacket packet) =>
        CookingRuntime.Handle(client, packet);
}
