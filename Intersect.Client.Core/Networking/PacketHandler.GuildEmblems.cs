using Intersect.Network;
using Intersect.Network.Packets.Guilds;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, GuildEmblemStatePacket packet)
    {
        if (Intersect.Client.Interface.Interface.HasInGameUI) Intersect.Client.Interface.Interface.GameUi.ApplyGuildEmblems(packet);
    }
}
