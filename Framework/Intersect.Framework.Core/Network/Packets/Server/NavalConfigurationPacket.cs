using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public sealed partial class NavalConfigurationPacket : IntersectPacket
{
    [Key(6)] public Guid RequestId { get; set; }
    [Key(0)] public string NavigationJson { get; set; } = "";
    [Key(1)] public string CatalogueJson { get; set; } = "";
    [Key(2)] public string NavigationVersion { get; set; } = "";
    [Key(3)] public string CatalogueVersion { get; set; } = "";
    [Key(4)] public byte SavedSection { get; set; }
    [Key(5)] public string Error { get; set; } = "";
}
