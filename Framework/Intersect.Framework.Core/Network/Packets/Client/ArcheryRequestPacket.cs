using MessagePack;
namespace Intersect.Network.Packets.Client;
public enum ArcheryAction { Open, Start, Draw, Release, Leave, Masteries, GetConfiguration, SaveConfiguration }
[MessagePackObject]
public sealed partial class ArcheryRequestPacket : IntersectPacket
{
    [Key(0)] public ArcheryAction Action { get; set; }
    [Key(1)] public Guid RangeId { get; set; }
    [Key(2)] public Guid MatchId { get; set; }
    [Key(3)] public long Sequence { get; set; }
    [Key(4)] public double Yaw { get; set; }
    [Key(5)] public double Pitch { get; set; }
    [Key(6)] public string ConfigurationJson { get; set; } = "";
    [Key(7)] public Guid RequestId { get; set; }
}
