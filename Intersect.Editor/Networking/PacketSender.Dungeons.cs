using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Networking;

public static partial class PacketSender
{
    public static void SendRequestDungeonConfiguration(bool openEditor)
    {
        Network.SendPacket(new RequestDungeonConfigurationPacket(openEditor));
    }

    public static void SendSaveDungeonConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveDungeonConfigurationPacket(configurationJson));
    }
}
