using Intersect.Editor.General;
using Intersect.Framework.Core.QuestShops;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, QuestShopConfigurationPacket packet)
    {
        QuestShopConfiguration.Load(packet.ConfigurationJson);

        if (packet.OpenEditor)
        {
            Globals.MainForm.BeginInvoke(
                (Action)(() => Globals.MainForm.OpenQuestShopEditor())
            );
        }
    }
}
