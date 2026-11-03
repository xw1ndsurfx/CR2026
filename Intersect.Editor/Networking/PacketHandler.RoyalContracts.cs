using Intersect.Editor.General;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, RoyalContractsConfigurationPacket packet) =>
        Globals.MainForm.BeginInvoke((Action)(() => Globals.MainForm.ApplyRoyalContractsConfiguration(packet)));
}
