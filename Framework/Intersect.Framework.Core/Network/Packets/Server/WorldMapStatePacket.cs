using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class WorldMapStatePacket : IntersectPacket
{
    [Key(0)] public WorldMapSocialMarker[] Players { get; set; } = [];
    [Key(1)] public string LegendJson { get; set; } = "{\"Entries\":[]}";
}

[MessagePackObject]
public sealed class WorldMapSocialMarker
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public Guid MapId { get; set; }
    [Key(3)] public int X { get; set; }
    [Key(4)] public int Y { get; set; }
    [Key(5)] public bool Party { get; set; }
}
