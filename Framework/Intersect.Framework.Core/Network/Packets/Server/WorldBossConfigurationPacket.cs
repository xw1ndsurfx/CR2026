using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject]
public partial class WorldBossConfigurationPacket : IntersectPacket
{
    public WorldBossConfigurationPacket() { }

    public WorldBossConfigurationPacket(string json, bool openEditor)
    {
        ConfigurationJson = json;
        OpenEditor = openEditor;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public bool OpenEditor { get; set; }
}
