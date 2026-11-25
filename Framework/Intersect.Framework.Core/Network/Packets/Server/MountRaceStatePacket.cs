using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class MountRaceStatePacket : IntersectPacket
{
    [Key(0)] public bool Editor { get; set; }
    [Key(1)] public bool Open { get; set; }
    [Key(2)] public bool Saved { get; set; }
    [Key(3)] public string Json { get; set; } = "";
    [Key(4)] public string Message { get; set; } = "";
    [Key(5)] public Guid RequestId { get; set; }
    [Key(6)] public string MountsJson { get; set; } = "";
}
