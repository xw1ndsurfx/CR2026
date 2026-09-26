using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class SaveProfessionConfigurationPacket : EditorPacket
{
    public SaveProfessionConfigurationPacket() { }
    public SaveProfessionConfigurationPacket(string configurationJson) => ConfigurationJson = configurationJson;
    [Key(0)] public string ConfigurationJson { get; set; } = string.Empty;
}
