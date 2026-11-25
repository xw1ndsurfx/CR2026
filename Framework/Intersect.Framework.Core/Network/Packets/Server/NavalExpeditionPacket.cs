using MessagePack;
namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class NavalExpeditionPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "{}";
    [Key(1)] public bool Open { get; set; }
}
