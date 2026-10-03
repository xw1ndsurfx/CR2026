using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class SaveDungeonConfigurationPacket : EditorPacket
{
    public SaveDungeonConfigurationPacket()
    {
    }

    public SaveDungeonConfigurationPacket(string configurationJson)
    {
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
