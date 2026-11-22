using Intersect.Network;
using Intersect.Network.Packets.RoyalSeasons;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, RoyalSeasonStatePacket packet) => Interface.Interface.EnqueueInGame(ui => ui.ApplyRoyalSeasons(packet));
}
