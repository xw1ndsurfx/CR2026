using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject(AllowPrivate = true)]
public partial class SaveInvasionConfigurationPacket : EditorPacket
{
    public SaveInvasionConfigurationPacket() { }

    public SaveInvasionConfigurationPacket(string configurationJson) =>
        ConfigurationJson = configurationJson;

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
