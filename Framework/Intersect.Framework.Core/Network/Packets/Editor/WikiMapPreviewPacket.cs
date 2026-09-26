using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class WikiMapPreviewPacket : EditorPacket
{
    public WikiMapPreviewPacket()
    {
    }

    public WikiMapPreviewPacket(Guid mapId, byte[] pngData)
    {
        MapId = mapId;
        PngData = pngData;
    }

    [Key(0)]
    public Guid MapId { get; set; }

    [Key(1)]
    public byte[] PngData { get; set; } = [];
}
