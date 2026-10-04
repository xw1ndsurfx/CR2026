using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Networking;

public static partial class PacketSender
{
    public static void SendRequestQuestShopConfiguration(bool openEditor)
    {
        Network.SendPacket(new RequestQuestShopConfigurationPacket(openEditor));
    }

    public static void SendSaveQuestShopConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveQuestShopConfigurationPacket(configurationJson));
    }
}
