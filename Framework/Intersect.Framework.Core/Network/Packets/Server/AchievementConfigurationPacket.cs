using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class AchievementConfigurationPacket : IntersectPacket
{
    public AchievementConfigurationPacket()
    {
    }

    public AchievementConfigurationPacket(string configurationJson, bool openEditor)
    {
        ConfigurationJson = configurationJson;
        OpenEditor = openEditor;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public bool OpenEditor { get; set; }
}
