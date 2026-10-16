using Intersect.Network;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors;
namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, WorldActivitiesPacket packet)
    { if (packet.OpenEditor) Globals.MainForm.BeginInvoke((Action)(() => new FrmWorldActivities(packet.Json).Show(Globals.MainForm))); }
}
