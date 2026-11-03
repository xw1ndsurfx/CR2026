using Intersect.Network.Packets.RoyalContracts;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,RoyalSealShopRequestPacket packet)
    {if(!client.IsEditor && client.Entity is {} player)Intersect.Server.RoyalContracts.RoyalSealShopRuntime.Request(player,packet);}
}
