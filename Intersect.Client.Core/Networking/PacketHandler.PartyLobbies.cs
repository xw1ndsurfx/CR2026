using Intersect.Network;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, PartyLobbyStatePacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(ui => ui.ApplyPartyLobbies(packet));
}
