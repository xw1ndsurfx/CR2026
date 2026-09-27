using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject(AllowPrivate = true)]
public partial class LogiCoinShopConfigurationPacket : IntersectPacket
{
    public LogiCoinShopConfigurationPacket()
    {
    }

    public LogiCoinShopConfigurationPacket(string configurationJson, bool openEditor = false)
    {
        ConfigurationJson = configurationJson;
        OpenEditor = openEditor;
    }

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public bool OpenEditor { get; set; }
}
