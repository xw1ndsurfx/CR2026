using Intersect.Network;
using Intersect.Client.WorldEvents.Invasions;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, InvasionStatusPacket packet)
    {
        InvasionEnvironmentManager.ApplyStatus(packet);

        if (!Intersect.Client.Interface.Interface.HasInGameUI) return;
        Intersect.Client.Interface.Interface.GameUi.UpdateInvasionStatus(packet);
    }

    public void HandlePacket(IPacketSender packetSender, InvasionResultPacket packet)
    {
        if (!Intersect.Client.Interface.Interface.HasInGameUI) return;
        Intersect.Client.Interface.Interface.GameUi.ShowInvasionResult(packet);
    }
}
