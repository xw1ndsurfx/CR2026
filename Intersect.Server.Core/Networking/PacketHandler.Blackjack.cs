using Intersect.Network.Packets.Client;
using Intersect.Server.MiniGames.Blackjack;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,BlackjackRequestPacket packet)=>BlackjackRuntime.Handle(client,packet);
}
