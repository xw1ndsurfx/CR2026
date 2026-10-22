using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender,ServiceShopPacket packet)=>
        Interface.Interface.EnqueueInGame(game=>game.ApplyServiceShop(packet.Json));
}
