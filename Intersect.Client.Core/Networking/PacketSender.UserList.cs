using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestBlockedUsers() =>
        Network.SendPacket(new RequestBlockedUsersPacket());

    public static void SendUpdateBlockedUser(bool blocked, string name, Guid id = default) =>
        Network.SendPacket(new UpdateBlockedUserPacket(blocked, name, id));
}
