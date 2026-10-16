using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class DeathConfigurationPacket : IntersectPacket
{
    [Key(0)] public string ConfigurationJson { get; set; } = "";
    [Key(1)] public string Error { get; set; } = "";
    [Key(2)] public bool Saved { get; set; }
}
