using Intersect.Network.Packets.Client;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, AdvancedCombatActionPacket packet)
    {
        if (client?.Entity is not { } player || packet == null)
        {
            return;
        }

        lock (player.EntityLock)
        {
            switch (packet.Action)
            {
                case AdvancedCombatAction.Dodge:
                    player.TryAdvancedDodge(packet.Direction);
                    break;
                case AdvancedCombatAction.CounterAttack:
                    player.TryAdvancedCounter();
                    break;
            }
        }
    }
}
