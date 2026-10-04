using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendAcceptQuestShopQuest(Guid shopId, Guid questId)
    {
        Network.SendPacket(new AcceptQuestShopQuestPacket(shopId, questId));
    }
}
