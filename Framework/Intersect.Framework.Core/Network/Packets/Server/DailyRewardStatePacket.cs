using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject(AllowPrivate = true)]
public sealed class DailyRewardPacketEntry
{
    [Key(0)] public int Day { get; set; }
    [Key(1)] public Guid ItemId { get; set; }
    [Key(2)] public int Quantity { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
public partial class DailyRewardStatePacket : IntersectPacket
{
    [Key(0)] public int CycleDays { get; set; }
    [Key(1)] public int CurrentDay { get; set; }
    [Key(2)] public bool CanClaim { get; set; }
    [Key(3)] public bool ClaimedToday { get; set; }
    [Key(4)] public long NextClaimUnixMs { get; set; }
    [Key(5)] public DailyRewardPacketEntry[] Rewards { get; set; } = [];
    [Key(6)] public string Message { get; set; } = "";
}
