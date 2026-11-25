using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, MountRaceStatePacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyMountRace(packet));
}
