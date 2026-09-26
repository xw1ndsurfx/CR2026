using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject]
public partial class InvasionConfigurationPacket : IntersectPacket
{
    public InvasionConfigurationPacket() { }

    public InvasionConfigurationPacket(string configurationJson, bool openEditor)
    {
        ConfigurationJson = configurationJson;
        OpenEditor = openEditor;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public bool OpenEditor { get; set; }
}
