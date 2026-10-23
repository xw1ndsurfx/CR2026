using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class MountStatePacket : IntersectPacket
{
    [Key(0)] public string ConfigurationJson { get; set; } = "";
    [Key(1)] public Guid[] Owned { get; set; } = [];
    [Key(2)] public Guid Active { get; set; }
    [Key(3)] public Guid Favorite { get; set; }
    [Key(4)] public bool OpenWindow { get; set; }
    [Key(5)] public string Message { get; set; } = "";
    [Key(6)] public bool Editor { get; set; }
    [Key(7)] public bool Saved { get; set; }
    [Key(8)] public Guid RequestId { get; set; }
}
