using Intersect.Network;
using Intersect.Network.Packets.Housing;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, HouseStatePacket packet)
    { if (Intersect.Client.Interface.Interface.HasInGameUI) Intersect.Client.Interface.Interface.GameUi.ApplyHouse(packet); }
}
