using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject(AllowPrivate = true)]
public partial class SaveRewardConfigurationPacket : EditorPacket
{
    public SaveRewardConfigurationPacket()
    {
    }

    public SaveRewardConfigurationPacket(string configurationJson)
    {
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = "";
}
