using Intersect.Network;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, IntoxicationPacket packet)
    { if (Intersect.Client.Interface.Interface.HasInGameUI) Intersect.Client.Interface.Game.IntoxicationVisuals.Apply(packet); }
}
