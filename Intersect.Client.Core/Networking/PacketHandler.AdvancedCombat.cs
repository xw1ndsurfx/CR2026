using Intersect.Client.Core;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CombatTelegraphPacket packet) =>
        Graphics.ReceiveAdvancedCombatTelegraph(packet);
}
