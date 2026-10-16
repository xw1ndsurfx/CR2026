using Intersect.Network;
using Intersect.Network.Packets.Housing;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, HousingConfigurationPacket packet)
    { if (packet.OpenWindow) Globals.MainForm.BeginInvoke((Action)(() => new FrmHousingConfiguration(packet.Json).Show(Globals.MainForm))); }
}
