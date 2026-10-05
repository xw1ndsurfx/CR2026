using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Networking;

public static partial class PacketSender
{
    public static void SendRequestRoyalStylistConfiguration(
        bool openEditor
    )
    {
        Network.SendPacket(
            new RequestRoyalStylistConfigurationPacket(openEditor)
        );
    }

    public static void SendSaveRoyalStylistConfiguration(
        string configurationJson
    )
    {
        Network.SendPacket(
            new SaveRoyalStylistConfigurationPacket(configurationJson)
        );
    }
}
