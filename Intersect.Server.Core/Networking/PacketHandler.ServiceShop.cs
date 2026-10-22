using Intersect.Network.Packets.Client;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,ServiceShopPurchasePacket packet)
    {if(!client.IsEditor && client.Entity is {} player)Shops.ServiceShopRuntime.Purchase(player,packet);}
}
