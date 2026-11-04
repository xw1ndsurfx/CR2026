using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class ExportationStatePacket : IntersectPacket
{
    [Key(0)] public bool Open { get; set; }
    [Key(1)] public bool Editor { get; set; }
    [Key(2)] public bool Saved { get; set; }
    [Key(3)] public string ConfigurationJson { get; set; } = "";
    [Key(4)] public string RouteJson { get; set; } = "";
    [Key(5)] public string RunJson { get; set; } = "";
    [Key(6)] public string MerchantName { get; set; } = "";
    [Key(7)] public string RecipientName { get; set; } = "";
    [Key(8)] public string SourceMapName { get; set; } = "";
    [Key(9)] public string DestinationMapName { get; set; } = "";
    [Key(10)] public bool HasUsableBoat { get; set; }
    [Key(11)] public int ProfessionLevel { get; set; }
    [Key(12)] public string Message { get; set; } = "";
    [Key(13)] public bool Close { get; set; }
}
