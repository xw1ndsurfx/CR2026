using Intersect.Network;
using Intersect.Network.Packets.Server;
using Intersect.Editor.General;
namespace Intersect.Editor.Networking;
internal partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, MountStatePacket packet)
    {
        if (!packet.Editor || Globals.MainForm is not { IsDisposed: false } main) return;
        main.BeginInvoke((Action)(() => main.ApplyMountConfiguration(packet)));
    }
}
