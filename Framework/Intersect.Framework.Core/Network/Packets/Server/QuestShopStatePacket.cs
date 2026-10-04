using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class QuestShopStatePacket : IntersectPacket
{
    public QuestShopStatePacket() { }

    public QuestShopStatePacket(Guid shopId, string shopName, string description, QuestShopEntryPacket[] entries, string message = "")
    {
        ShopId = shopId;
        ShopName = shopName;
        Description = description;
        Entries = entries;
        Message = message;
    }

    [Key(0)] public Guid ShopId { get; set; }
    [Key(1)] public string ShopName { get; set; } = string.Empty;
    [Key(2)] public string Description { get; set; } = string.Empty;
    [Key(3)] public QuestShopEntryPacket[] Entries { get; set; } = [];
    [Key(4)] public string Message { get; set; } = string.Empty;
}

[MessagePackObject]
public partial class QuestShopEntryPacket
{
    public QuestShopEntryPacket() { }

    public QuestShopEntryPacket(Guid questId, string name, string description, string status, bool canAccept)
    {
        QuestId = questId;
        Name = name;
        Description = description;
        Status = status;
        CanAccept = canAccept;
    }

    [Key(0)] public Guid QuestId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public string Description { get; set; } = string.Empty;
    [Key(3)] public string Status { get; set; } = string.Empty;
    [Key(4)] public bool CanAccept { get; set; }
}
