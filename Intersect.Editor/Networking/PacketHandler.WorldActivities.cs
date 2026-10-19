using Intersect.Network;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors;
namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, WorldActivitiesPacket packet)
    {
        if (packet.RequestId != Guid.Empty)
        {
            Globals.MainForm?.BeginInvoke((Action)(() => ActivitySaveSession.Receive(packet)));
            return;
        }
        if (packet.OpenEditor)
            Globals.MainForm.BeginInvoke((Action)(() => (packet.Section == Intersect.Framework.Core.WorldEvents.WorldActivitiesSection.Metalworking ? (System.Windows.Forms.Form)new FrmMetalworking(packet.Json, Globals.MainForm.RequestedMetalworkKind) : packet.Section == Intersect.Framework.Core.WorldEvents.WorldActivitiesSection.Pvp ? (System.Windows.Forms.Form)new FrmPvpConfiguration(packet.Json) : new FrmWorldActivities(packet.Json, packet.Section)).Show(Globals.MainForm)));
    }
}
