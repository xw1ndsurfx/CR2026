using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestWorldBossStatus() =>
        Network.SendPacket(new RequestWorldBossStatusPacket());
}
