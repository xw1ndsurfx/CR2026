using MessagePack;
namespace Intersect.Network.Packets.Housing;
public enum HouseAction { Open, Visit, Leave, SetPublic, Buy, Place, Remove, StorePet, CollectPet }
[MessagePackObject]
public partial class HouseActionPacket : IntersectPacket
{
    [Key(0)] public HouseAction Action { get; set; }
    [Key(1)] public Guid OwnerId { get; set; }
    [Key(2)] public Guid Id { get; set; }
    [Key(3)] public Guid PlacementId { get; set; }
    [Key(4)] public int X { get; set; }
    [Key(5)] public int Y { get; set; }
    [Key(6)] public int Rotation { get; set; }
    [Key(7)] public bool Public { get; set; }
}
[MessagePackObject]
public partial class HouseStatePacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = string.Empty;
    [Key(1)] public bool OpenWindow { get; set; }
    [Key(2)] public string Message { get; set; } = string.Empty;
    [Key(3)] public string WorldJson { get; set; } = string.Empty;
}
[MessagePackObject]
public partial class HousingConfigurationPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = string.Empty;
    [Key(1)] public bool OpenWindow { get; set; }
}
[MessagePackObject]
public partial class RequestHousingConfigurationPacket : IntersectPacket { }
[MessagePackObject]
public partial class SaveHousingConfigurationPacket : IntersectPacket { [Key(0)] public string Json { get; set; } = string.Empty; }
