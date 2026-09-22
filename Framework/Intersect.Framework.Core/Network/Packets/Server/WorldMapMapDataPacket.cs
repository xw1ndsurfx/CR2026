using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class WorldMapMapDataPacket : IntersectPacket
{
    public WorldMapMapDataPacket()
    {
    }

    public WorldMapMapDataPacket(Guid mapId, string data, byte[] tileData, int revision)
    {
        MapId = mapId;
        Data = data;
        TileData = tileData;
        Revision = revision;
    }

    [Key(0)]
    public Guid MapId { get; set; }

    [Key(1)]
    public string Data { get; set; } = string.Empty;

    [Key(2)]
    public byte[] TileData { get; set; } = [];

    [Key(3)]
    public int Revision { get; set; }
}
