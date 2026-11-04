using Intersect.Editor.General;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, ExportationStatePacket packet)
    {
        if (!packet.Editor || Globals.MainForm is not { IsDisposed: false, IsHandleCreated: true } main) return;
        try
        {
            main.BeginInvoke((Action)(() =>
            {
                if (!main.IsDisposed) main.ApplyExportationConfiguration(packet);
            }));
        }
        catch (InvalidOperationException) when (main.IsDisposed || !main.IsHandleCreated) { }
    }
}
