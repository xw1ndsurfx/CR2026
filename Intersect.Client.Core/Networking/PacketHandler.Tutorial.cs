using Intersect.Network;
using Intersect.Network.Packets.Tutorials;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, TutorialStatePacket packet) => Interface.Interface.EnqueueInGame(ui => ui.ApplyTutorial(packet));
}
