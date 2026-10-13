using Intersect.Enums;
using Intersect.Network.Packets.Client;
using Intersect.Server.Entities;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestBlockedUsersPacket packet)
    {
        if (client?.Entity is { } player)
            PacketSender.SendBlockedUsers(player);
    }

    public void HandlePacket(Client client, UpdateBlockedUserPacket packet)
    {
        var player = client?.Entity;
        if (player == null)
            return;

        if (packet.Blocked)
        {
            var name = packet.CharacterName?.Trim() ?? string.Empty;
            if (name.Length is < 3 or > 48)
                return;

            var target = Player.Find(name);
            if (target == null || target.Id == player.Id)
            {
                PacketSender.SendChatMsg(player, "Character not found or cannot be blocked.", ChatMessageType.Notice);
                return;
            }

            if (!player.TryBlock(target))
                PacketSender.SendChatMsg(player, "Unable to block character: list is full.", ChatMessageType.Notice);
        }
        else
        {
            // The id comes from the player's own authoritative list. A
            // malicious request cannot unblock another character's list.
            if (!player.TryUnblock(packet.TargetId))
                PacketSender.SendChatMsg(player, "That character is not on your block list.", ChatMessageType.Notice);
        }

        PacketSender.SendBlockedUsers(player);
    }
}
