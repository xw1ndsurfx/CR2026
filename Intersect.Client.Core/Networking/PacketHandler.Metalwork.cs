using Intersect.Network;
using Intersect.Network.Packets.Metalworking;
namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, MetalworkStatePacket packet) => Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyMetalwork(packet));
}
