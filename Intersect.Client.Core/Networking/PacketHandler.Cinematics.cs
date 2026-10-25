using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CinematicCameraPacket packet) => Cinematics.CinematicCamera.Receive(packet);
    public void HandlePacket(IPacketSender sender, CinematicControlPacket packet) => Cinematics.CinematicControls.Receive(packet);
    public void HandlePacket(IPacketSender sender, CinematicFleetPacket packet) => Naval.NavalWorld.ApplyCinematicFleet(packet);
}
