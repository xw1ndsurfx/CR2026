using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, JoustStatePacket packet)
    {
        if (!packet.Editor) Interface.Interface.EnqueueInGame(game => game.ApplyJoustState(packet));
    }
}
