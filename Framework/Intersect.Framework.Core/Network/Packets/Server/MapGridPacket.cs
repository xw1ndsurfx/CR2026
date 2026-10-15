using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class MapGridPacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public MapGridPacket()
    {
    }

    public MapGridPacket(
        Guid[,] grid,
        string[,] editorGrid,
        bool clearKnownMaps,
        WorldMapEventMarker[]? worldMapEventMarkers = null
    )
    {
        Grid = grid;
        EditorGrid = editorGrid;
        ClearKnownMaps = clearKnownMaps;
        WorldMapEventMarkers = worldMapEventMarkers ?? [];
    }

    [Key(0)]
    public Guid[,] Grid { get; set; }

    [Key(1)]
    public string[,] EditorGrid { get; set; }

    [Key(2)]
    public bool ClearKnownMaps { get; set; }

    [Key(3)]
    public WorldMapEventMarker[] WorldMapEventMarkers { get; set; } = [];

}

[MessagePackObject]
public sealed class WorldMapEventMarker
{
    public WorldMapEventMarker()
    {
    }

    public WorldMapEventMarker(Guid eventId, Guid mapId, int x, int y, Guid animationId, int offsetX = 0, int offsetY = 0)
    {
        EventId = eventId;
        MapId = mapId;
        X = x;
        Y = y;
        AnimationId = animationId;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    [Key(0)]
    public Guid EventId { get; set; }

    [Key(1)]
    public Guid MapId { get; set; }

    [Key(2)]
    public int X { get; set; }

    [Key(3)]
    public int Y { get; set; }

    [Key(4)]
    public Guid AnimationId { get; set; }

    [Key(5)] public int OffsetX { get; set; }
    [Key(6)] public int OffsetY { get; set; }
    [Key(7)] public string Name { get; set; } = string.Empty;
}
