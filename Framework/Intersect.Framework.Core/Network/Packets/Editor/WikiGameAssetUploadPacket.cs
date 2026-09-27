using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class WikiGameAssetUploadPacket : EditorPacket
{
    public WikiGameAssetUploadPacket()
    {
    }

    public WikiGameAssetUploadPacket(string category, Guid objectId, byte[] pngData)
    {
        Category = category;
        ObjectId = objectId;
        PngData = pngData;
    }

    [Key(0)]
    public string Category { get; set; } = string.Empty;

    [Key(1)]
    public Guid ObjectId { get; set; }

    [Key(2)]
    public byte[] PngData { get; set; } = [];
}
