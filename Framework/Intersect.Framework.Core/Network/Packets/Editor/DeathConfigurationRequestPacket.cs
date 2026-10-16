using MessagePack;
namespace Intersect.Network.Packets.Editor;
[MessagePackObject]
public partial class DeathConfigurationRequestPacket : EditorPacket
{
    [Key(0)] public string? ConfigurationJson { get; set; }
}
