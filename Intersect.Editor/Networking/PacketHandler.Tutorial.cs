using Intersect.Network;
using Intersect.Network.Packets.Tutorials;
using Intersect.Editor.General;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, TutorialEditorPacket packet) => Globals.MainForm.BeginInvoke((Action)(() => Globals.MainForm.ReceiveTutorial(packet)));
}
