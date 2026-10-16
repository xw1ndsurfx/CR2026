using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public sealed class ContentTranslationsPacket : IntersectPacket
{
    [Key(0)] public string CatalogJson { get; set; } = "";
    [Key(1)] public bool Replace { get; set; }
}
