using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject(AllowPrivate = true)]
public partial class SaveLogiCoinShopConfigurationPacket : EditorPacket
{
    public SaveLogiCoinShopConfigurationPacket()
    {
    }

    public SaveLogiCoinShopConfigurationPacket(string configurationJson)
    {
        ConfigurationJson = configurationJson;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
