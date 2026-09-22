using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject(AllowPrivate = true)]
public partial class RewardConfigurationPacket : IntersectPacket
{
    public RewardConfigurationPacket()
    {
    }

    public RewardConfigurationPacket(string configurationJson, bool openEditor = false)
    {
        ConfigurationJson = configurationJson;
        OpenEditor = openEditor;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = "";

    [Key(1)]
    public bool OpenEditor { get; set; }
}
