using Intersect.Network;
using Intersect.Network.Packets.GuildHeadquarters;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, HeadquartersStatePacket packet) => Interface.Interface.EnqueueInGame(ui => ui.ApplyHeadquarters(packet));
    public void HandlePacket(IPacketSender sender, HeadquartersInvitationPacket packet) => Interface.Interface.EnqueueInGame(ui => ui.InviteToHeadquarters(packet));
}
