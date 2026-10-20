using MessagePack;
namespace Intersect.Network.Packets.Editor;
[MessagePackObject]
public sealed partial class NavalConfigurationRequestPacket : EditorPacket
{
    // 0 read, 1 navigation, 2 shop. Each server file is saved atomically and independently.
    [Key(3)] public Guid RequestId { get; set; } = Guid.NewGuid();
    [Key(0)] public byte Section { get; set; }
    [Key(1)] public string ConfigurationJson { get; set; } = "";
    [Key(2)] public string ExpectedVersion { get; set; } = "";
}
