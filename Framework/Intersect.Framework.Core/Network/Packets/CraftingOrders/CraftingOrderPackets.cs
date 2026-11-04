using MessagePack;

namespace Intersect.Network.Packets
{
    [MessagePackObject]
    public sealed class CraftingOrderView
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public Guid ItemId { get; set; }
        [Key(2)] public int Quantity { get; set; }
        [Key(3)] public int Reward { get; set; }
        [Key(4)] public string Customer { get; set; } = string.Empty;
        [Key(5)] public string Crafter { get; set; } = string.Empty;
        [Key(6)] public byte Status { get; set; }
        [Key(7)] public bool IsCustomer { get; set; }
        [Key(8)] public bool IsCrafter { get; set; }
        [Key(9)] public long DeadlineUtcMs { get; set; }
    }
}

namespace Intersect.Network.Packets.Client
{
    [MessagePackObject]
    public partial class RequestCraftingOrdersPacket : IntersectPacket { }

    [MessagePackObject]
    public partial class PostCraftingOrderPacket : IntersectPacket
    {
        [Key(0)] public Guid ItemId { get; set; }
        [Key(1)] public int Quantity { get; set; }
        [Key(2)] public int Reward { get; set; }
        [Key(3)] public int DurationHours { get; set; }
    }

    [MessagePackObject]
    public partial class CraftingOrderActionPacket : IntersectPacket
    {
        [Key(0)] public Guid OrderId { get; set; }
        // 0 accept, 1 deliver, 2 cancel
        [Key(1)] public byte Action { get; set; }
    }
}

namespace Intersect.Network.Packets.Server
{
    [MessagePackObject]
    public partial class CraftingOrdersStatePacket : IntersectPacket
    {
        [Key(0)] public CraftingOrderView[] Orders { get; set; } = [];
        [Key(1)] public Guid[] CraftableItemIds { get; set; } = [];
        [Key(2)] public int Aureons { get; set; }
        [Key(3)] public string MessageKey { get; set; } = string.Empty;
        [Key(4)] public bool OpenWindow { get; set; }
    }
}
