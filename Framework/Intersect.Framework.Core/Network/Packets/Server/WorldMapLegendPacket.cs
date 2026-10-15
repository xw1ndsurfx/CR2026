using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class WorldMapLegendPacket : IntersectPacket
{
    [Key(0)] public string LegendJson { get; set; } = "{\"Entries\":[]}";
    [Key(1)] public bool OpenEditor { get; set; }
}
