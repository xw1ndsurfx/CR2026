using Intersect.Editor.General;
using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, DeathConfigurationPacket packet) =>
        Globals.MainForm.BeginInvoke((Action)(() => Globals.MainForm.ReceiveDeathConfiguration(packet)));
}
