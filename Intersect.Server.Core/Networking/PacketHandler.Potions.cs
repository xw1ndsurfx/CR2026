using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Potions;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PotionRequestPacket packet) => PotionRuntime.Handle(client, packet);
}
