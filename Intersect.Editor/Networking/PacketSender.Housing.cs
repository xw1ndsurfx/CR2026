using Intersect.Network.Packets.Housing;
namespace Intersect.Editor.Networking;
public static partial class PacketSender
{
    public static void SendHousingConfiguration(string json) => Network.SendPacket(new SaveHousingConfigurationPacket { Json = json });
    public static void RequestHousingConfiguration() => Network.SendPacket(new RequestHousingConfigurationPacket());
}
