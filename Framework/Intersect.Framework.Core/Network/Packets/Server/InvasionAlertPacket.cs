using MessagePack;
namespace Intersect.Network.Packets.WorldEvents;
[MessagePackObject]
public partial class InvasionAlertPacket : IntersectPacket
{
    [Key(0)] public Guid InvasionId { get; set; }
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public int MinutesBefore { get; set; }
    [Key(4)] public string Island { get; set; } = "";
    [Key(5)] public string Target { get; set; } = "";
    [Key(6)] public string StartTime { get; set; } = "";
}
