using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class CookingStatePacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public Guid PlayerId { get; set; }
    [Key(2)] public long Sequence { get; set; }
    [Key(3)] public long RequestId { get; set; }
    [Key(4)] public long ServerUnixMs { get; set; }
    [Key(5)] public bool Closed { get; set; }
    [Key(6)] public string ErrorCode { get; set; } = string.Empty;
    [Key(7)] public CookingSessionState? State { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        PlayerId != Guid.Empty &&
        Sequence > 0 &&
        RequestId >= 0 &&
        ServerUnixMs > 0 &&
        ErrorCode is { Length: <= 160 } &&
        (Closed ? State == null : State is { IsValid: true });
}
