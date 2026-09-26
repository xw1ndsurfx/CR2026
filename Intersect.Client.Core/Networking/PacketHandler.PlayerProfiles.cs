using Intersect.Client.Interface;
using Intersect.Enums;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, PlayerProfilePacket packet)
    {
        if (!packet.Found)
        {
            Interface.Interface.ShowAlert(
                string.IsNullOrWhiteSpace(packet.Message) ? "Character information is unavailable." : packet.Message,
                "Character Information",
                AlertType.Error
            );
            return;
        }

        Interface.Interface.EnqueueInGame(
            gameInterface =>
            {
                if (packet.IsSelf)
                    gameInterface.ApplyOwnPlayerProfile(packet);

                if (packet.OpenWindow)
                    gameInterface.ShowPlayerProfile(packet);
            }
        );
    }
}
