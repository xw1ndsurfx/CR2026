using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestPlayerProfile(Guid playerId, bool openWindow = true)
    {
        Network.SendPacket(new RequestPlayerProfilePacket(playerId, string.Empty, openWindow));
    }

    public static void SendRequestPlayerProfile(string playerName, bool openWindow = true)
    {
        Network.SendPacket(new RequestPlayerProfilePacket(Guid.Empty, playerName ?? string.Empty, openWindow));
    }
}
