using Intersect.Network;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, MatchmakingStatePacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyMatchmaking(packet));
}
