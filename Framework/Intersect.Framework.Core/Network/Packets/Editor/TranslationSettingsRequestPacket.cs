using MessagePack;
namespace Intersect.Network.Packets.Editor;
[MessagePackObject]
public sealed class TranslationSettingsRequestPacket : EditorPacket
{
    [Key(0)] public string Action { get; set; } = "get";
    [Key(1)] public string SettingsJson { get; set; } = "";
    [Key(2)] public string CatalogJson { get; set; } = "";
}
