using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Dice;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, DiceRequestPacket packet) =>
        DiceRuntime.Handle(client, packet);
}
