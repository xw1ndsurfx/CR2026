using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CinematicCameraPacket packet) => Cinematics.CinematicCamera.Receive(packet);
}
