using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CampfiresPacket packet) =>
        Interface.Interface.EnqueueInGame(game => game.ApplyCampfires(packet));
}
