using Intersect.Network;
using Intersect.Network.Packets.RoyalSeasons;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    private FrmRoyalSeasons? _royalSeasonEditor;
    public void HandlePacket(IPacketSender sender, RoyalSeasonConfigurationPacket packet)
    {
        Globals.MainForm.BeginInvoke((Action)(() =>
        {
            if (packet.Open)
            {
                if (_royalSeasonEditor is { IsDisposed: false }) { _royalSeasonEditor.Activate(); return; }
                _royalSeasonEditor = new(packet); _royalSeasonEditor.Show(Globals.MainForm);
            }
            else if (_royalSeasonEditor is { IsDisposed: false }) _royalSeasonEditor.Acknowledge(packet);
        }));
    }
}
