using Intersect.Client.Interface;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, ProfessionProgressPacket packet)
    {
        Interface.Interface.EnqueueInGame(
            gameInterface => gameInterface.ShowProfessionProgress(packet)
        );
    }
}
