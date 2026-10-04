using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class AcceptQuestShopQuestPacket : IntersectPacket
{
    public AcceptQuestShopQuestPacket() { }
    public AcceptQuestShopQuestPacket(Guid shopId, Guid questId)
    {
        ShopId = shopId;
        QuestId = questId;
    }

    [Key(0)] public Guid ShopId { get; set; }
    [Key(1)] public Guid QuestId { get; set; }
}
