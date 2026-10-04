using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestQuestShopOffer(Guid shopId, Guid questId)
    {
        Network.SendPacket(new RequestQuestShopOfferPacket(shopId, questId));
    }
}
