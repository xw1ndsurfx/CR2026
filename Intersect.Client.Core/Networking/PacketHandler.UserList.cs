using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, BlockedUsersPacket packet)
    {
        if (!Interface.Interface.HasInGameUI)
            return;

        Interface.Interface.EnqueueInGame(game => game.GameMenu.ApplyBlockedUsers(packet));
    }
}
