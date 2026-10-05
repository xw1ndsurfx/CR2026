using Intersect.Editor.General;
using Intersect.Framework.Core.RoyalStylist;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(
        IPacketSender packetSender,
        RoyalStylistConfigurationPacket packet
    )
    {
        RoyalStylistConfiguration.Load(packet.ConfigurationJson);

        if (packet.OpenEditor)
        {
            Globals.MainForm.BeginInvoke(
                (Action)(() =>
                    Globals.MainForm.OpenRoyalStylistEditor())
            );
        }
    }
}
