using Intersect.Network;
using Intersect.Network.Packets.Pvp;
namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, PvpStatePacket packet) => Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyPvp(packet));
}
