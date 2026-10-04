using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestDungeonPanel(bool openWindow = true)
    {
        Network.SendPacket(new RequestDungeonPanelPacket(openWindow));
    }

    public static void SendDungeonConfirmationResponse(Guid eventId, bool accept)
    {
        Network.SendPacket(new EventResponsePacket(eventId, accept ? (byte)1 : (byte)2));
    }

    public static void SendDungeonRetryResponse(Guid retryId, bool accept)
    {
        Network.SendPacket(new DungeonRetryResponsePacket(retryId, accept));
    }
}
