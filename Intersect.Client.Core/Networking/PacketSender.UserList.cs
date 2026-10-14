using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestGuildDirectory(string search, int page = 0) =>
        Network.SendPacket(new RequestGuildDirectoryPacket(search, page));

    public static void SendRequestGuildJoin(Guid guildId) =>
        Network.SendPacket(new RequestGuildJoinPacket(guildId));

    public static void SendRequestBlockedUsers() =>
        Network.SendPacket(new RequestBlockedUsersPacket());

    public static void SendUpdateBlockedUser(bool blocked, string name, Guid id = default) =>
        Network.SendPacket(new UpdateBlockedUserPacket(blocked, name, id));
}
