using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject(AllowPrivate = true)]
public partial class SaveWorldBossConfigurationPacket : EditorPacket
{
    public SaveWorldBossConfigurationPacket() { }

    public SaveWorldBossConfigurationPacket(string json) => ConfigurationJson = json;

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
