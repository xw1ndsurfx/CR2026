using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class SaveAchievementConfigurationPacket : EditorPacket
{
    public SaveAchievementConfigurationPacket()
    {
    }

    public SaveAchievementConfigurationPacket(string configurationJson)
    {
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
