using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class PlaySoundSequencePacket : IntersectPacket
{
    [Key(0)] public string First { get; set; } = string.Empty;
    [Key(1)] public string Second { get; set; } = string.Empty;
}
