using MessagePack;
namespace Intersect.Network.Packets.Housing;
[MessagePackObject]
public partial class HouseDirectoryEntry
{
    [Key(0)] public Guid OwnerId { get; set; }
    [Key(1)] public string OwnerName { get; set; } = "";
}
[MessagePackObject]
public partial class HouseDirectoryPacket : IntersectPacket
{
    [Key(0)] public HouseDirectoryEntry[] Houses { get; set; } = [];
    [Key(1)] public int Page { get; set; }
    [Key(2)] public bool HasMore { get; set; }
}
