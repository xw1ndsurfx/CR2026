using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestDungeonPanel(bool openWindow = true)
    {
        Network.SendPacket(new RequestDungeonPanelPacket(openWindow));
    }
}
