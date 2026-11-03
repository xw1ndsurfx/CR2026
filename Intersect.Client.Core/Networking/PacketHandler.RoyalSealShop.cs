using Intersect.Network;
using Intersect.Network.Packets.RoyalContracts;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender,RoyalSealShopStatePacket packet)
    {if(Intersect.Client.Interface.Interface.HasInGameUI)Intersect.Client.Interface.Interface.GameUi.ApplyRoyalSealShop(packet);}
}
