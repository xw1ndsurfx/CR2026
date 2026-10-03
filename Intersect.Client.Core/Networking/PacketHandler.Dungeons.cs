using Intersect.Client.Interface;
using Intersect.Framework.Core.Dungeons;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, DungeonStatePacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);
        Interface.Interface.EnqueueInGame(
            gameInterface => gameInterface.ApplyDungeonState(packet)
        );
    }

    public void HandlePacket(IPacketSender packetSender, DungeonRunStatePacket packet)
    {
        Interface.Interface.EnqueueInGame(
            gameInterface => gameInterface.ApplyDungeonRunState(packet)
        );
    }
}
