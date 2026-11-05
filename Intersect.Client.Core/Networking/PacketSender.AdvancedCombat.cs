using Intersect.Enums;
using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendAdvancedCombatAction(AdvancedCombatAction action, Direction direction = Direction.None) =>
        Network.SendPacket(new AdvancedCombatActionPacket(action, direction));
}
