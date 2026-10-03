using Intersect.Editor.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, DungeonConfigurationPacket packet)
    {
        DungeonConfiguration.Load(packet.ConfigurationJson);

        if (packet.OpenEditor)
        {
            Globals.MainForm.BeginInvoke(
                (Action)(() => Globals.MainForm.OpenDungeonEditor())
            );
        }
    }
}
