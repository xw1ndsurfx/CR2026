using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public sealed partial class JoustStatePacket : IntersectPacket
{
    [Key(0)] public string ViewJson { get; set; } = "";
    [Key(1)] public string ConfigurationJson { get; set; } = "";
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public bool Open { get; set; }
    [Key(4)] public bool Editor { get; set; }
    [Key(5)] public bool Saved { get; set; }
    [Key(6)] public Guid RequestId { get; set; }
    [Key(7)] public string MountsJson { get; set; } = "";
    [Key(8)] public string CareerJson { get; set; } = "";
    [Key(9)] public string DuelJson { get; set; } = "";
    [Key(10)] public bool OpenDuelWindow { get; set; }
}
