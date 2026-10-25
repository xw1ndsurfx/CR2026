using MessagePack;
namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class CinematicControlPacket : IntersectPacket
{
    [Key(0)] public Guid EventId { get; set; }
    [Key(1)] public long RunVersion { get; set; }
    [Key(2)] public bool Active { get; set; }
    [Key(3)] public bool AllowVisualSkip { get; set; }
    [Key(4)] public bool Skipping { get; set; }
    [Key(5)] public Guid CloseDialogueToken { get; set; }
    [Key(6)] public Guid PageId { get; set; }
}
