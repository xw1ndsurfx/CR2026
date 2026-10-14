using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;

namespace Intersect.Server.Networking;

public static partial class PacketSender
{
    public static void SendBlockedUsers(Player player)
    {
        if (player == null)
            return;

        var entries = player.ReadBlockedUsers()
            .OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new BlockedUserEntry(pair.Key, pair.Value))
            .ToArray();
        player.SendPacket(new BlockedUsersPacket(entries));
    }
}
