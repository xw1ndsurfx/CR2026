using MessagePack;
namespace Intersect.Network.Packets.Client;
public enum MountAction { List, Toggle, Favorite, GetConfiguration, SaveConfiguration }
[MessagePackObject]
public partial class MountRequestPacket : IntersectPacket
{
    [Key(0)] public MountAction Action { get; set; }
    [Key(1)] public Guid MountId { get; set; }
    [Key(2)] public string ConfigurationJson { get; set; } = "";
    [Key(3)] public Guid RequestId { get; set; }
}
