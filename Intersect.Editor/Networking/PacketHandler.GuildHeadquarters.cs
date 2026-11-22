using Intersect.Network;
using Intersect.Network.Packets.GuildHeadquarters;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    private FrmGuildHeadquarters? _headquartersEditor;
    public void HandlePacket(IPacketSender sender, HeadquartersConfigurationPacket packet)
    {
        Globals.MainForm.BeginInvoke((Action)(() =>
        {
            if (packet.Open)
            {
                if (_headquartersEditor is { IsDisposed: false }) { _headquartersEditor.Activate(); return; }
                _headquartersEditor = new(packet.Json, packet.Revision); _headquartersEditor.Show(Globals.MainForm);
            }
            else if (_headquartersEditor is { IsDisposed: false }) _headquartersEditor.Acknowledge(packet);
        }));
    }
}
