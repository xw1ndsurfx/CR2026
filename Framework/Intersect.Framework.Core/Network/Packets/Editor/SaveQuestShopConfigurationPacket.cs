using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class SaveQuestShopConfigurationPacket : IntersectPacket
{
    public SaveQuestShopConfigurationPacket() { }
    public SaveQuestShopConfigurationPacket(string configurationJson) => ConfigurationJson = configurationJson;

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
