using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class DungeonRewardNotificationPacket : IntersectPacket
{
    public DungeonRewardNotificationPacket()
    {
    }

    public DungeonRewardNotificationPacket(
        string dungeonName,
        long experience,
        Guid itemId,
        int itemQuantity
    )
    {
        DungeonName = dungeonName;
        Experience = experience;
        ItemId = itemId;
        ItemQuantity = itemQuantity;
    }

    [Key(0)] public string DungeonName { get; set; } = string.Empty;
    [Key(1)] public long Experience { get; set; }
    [Key(2)] public Guid ItemId { get; set; }
    [Key(3)] public int ItemQuantity { get; set; }
}
