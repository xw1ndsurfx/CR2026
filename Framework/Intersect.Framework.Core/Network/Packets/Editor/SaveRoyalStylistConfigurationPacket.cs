using MessagePack;

namespace Intersect.Network.Packets.Editor;

[MessagePackObject]
public partial class SaveRoyalStylistConfigurationPacket : IntersectPacket
{
    public SaveRoyalStylistConfigurationPacket()
    {
    }

    public SaveRoyalStylistConfigurationPacket(string configurationJson) =>
        ConfigurationJson = configurationJson;

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;
}
