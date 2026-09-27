using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, MarketplaceStatePacket packet)
    {
        global::Intersect.Client.Interface.Interface.GameUi.ApplyMarketplaceState(packet);
    }
}
