using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class PotionMiniGamePacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public int Seed { get; set; }
    [Key(2)] public string Title { get; set; } = "Royal Alchemy";

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        Seed != 0 &&
        Title is { Length: >= 1 and <= 64 };
}
