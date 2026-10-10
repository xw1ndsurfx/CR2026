using Intersect.Network;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, WorldBossStatusPacket packet)
    {
        if (!Intersect.Client.Interface.Interface.HasInGameUI) return;
        Intersect.Client.Interface.Interface.GameUi.UpdateWorldBossStatus(packet);
    }

    public void HandlePacket(IPacketSender packetSender, WorldBossResultPacket packet)
    {
        if (!Intersect.Client.Interface.Interface.HasInGameUI) return;
        Intersect.Client.Interface.Interface.GameUi.ShowWorldBossResult(packet);
    }
}
