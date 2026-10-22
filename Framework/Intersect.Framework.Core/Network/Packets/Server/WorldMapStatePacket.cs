using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class WorldMapStatePacket : IntersectPacket
{
    [Key(0)] public WorldMapSocialMarker[] Players { get; set; } = [];
    [Key(1)] public string LegendJson { get; set; } = "{\"Entries\":[]}";
    // Null means this lightweight social refresh did not request naval positions.
    [Key(2)] public WorldMapShipMarker[]? Ships { get; set; } = [];
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
    [Key(6)] public bool Guild { get; set; }
}

[MessagePackObject]
public sealed class WorldMapShipMarker
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public string Name { get; set; } = "";
    [Key(3)] public int Level { get; set; }
    [Key(4)] public double X { get; set; }
    [Key(5)] public double Y { get; set; }
    [Key(6)] public bool Enemy { get; set; }
    [Key(7)] public double Heading { get; set; }
}
