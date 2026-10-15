using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class RequestWorldMapLegendPacket : IntersectPacket { }

[MessagePackObject]
public partial class SaveWorldMapLegendPacket : IntersectPacket
{
    [Key(0)] public string LegendJson { get; set; } = string.Empty;
}
