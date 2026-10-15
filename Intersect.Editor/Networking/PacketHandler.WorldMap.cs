using Intersect.Editor.Forms.Editors;
using Intersect.Editor.General;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, WorldMapLegendPacket packet)
    {
        var legend = WorldMapLegend.FromJson(packet.LegendJson);
        Globals.MainForm.BeginInvoke((Action)(() =>
        {
            if (packet.OpenEditor)
            {
                var editor = Application.OpenForms.OfType<FrmWorldMapLegend>().FirstOrDefault();
                if (editor == null) new FrmWorldMapLegend(legend).Show(Globals.MainForm);
                else editor.BringToFront();
            }
            else
                foreach (var editor in Application.OpenForms.OfType<FrmWorldMapLegend>()) editor.MarkSaved();
        }));
    }
}
