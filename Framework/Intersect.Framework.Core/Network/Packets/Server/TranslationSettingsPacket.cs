using MessagePack;
namespace Intersect.Network.Packets.Server;
// Sent only to authenticated editors; contains private service configuration.
[MessagePackObject]
public sealed class TranslationSettingsPacket : IntersectPacket
{
    [Key(0)] public string SettingsJson { get; set; } = "";
    [Key(1)] public string CatalogJson { get; set; } = "";
    [Key(2)] public string Status { get; set; } = "";
}
