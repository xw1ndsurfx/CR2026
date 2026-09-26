using Intersect.Network;
using Intersect.Network.Packets.Server;
using ClientInterface = Intersect.Client.Interface.Interface;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CookingStatePacket packet)
    {
        if (packet.IsValid)
            ClientInterface.GameUi?.QueueCookingState(packet);
    }
}
