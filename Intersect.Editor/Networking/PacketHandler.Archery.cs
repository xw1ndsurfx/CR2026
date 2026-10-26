using Intersect.Network;
using Intersect.Network.Packets.Server;
using Intersect.Editor.General;
namespace Intersect.Editor.Networking;
internal partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, ArcheryStatePacket packet)
    {
        if (packet.Editor && Globals.MainForm is { IsDisposed: false } main)
            main.BeginInvoke((Action)(() => main.ApplyRoyalCombatConfiguration(packet)));
    }
}
