using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class LockpickingStatePacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public Guid EventId { get; set; }
    [Key(2)] public long Sequence { get; set; }
    [Key(3)] public long RequestId { get; set; }
    [Key(4)] public bool Closed { get; set; }
    [Key(5)] public bool Success { get; set; }
    [Key(6)] public int Difficulty { get; set; }
    [Key(7)] public int MaxMistakes { get; set; }
    [Key(8)] public int Mistakes { get; set; }
    [Key(9)] public int TimeLimitSeconds { get; set; }
    [Key(10)] public int RemainingMilliseconds { get; set; }
    [Key(11)] public int TurnPercent { get; set; }
    [Key(12)] public string Hint { get; set; } = string.Empty;
    [Key(13)] public string ErrorCode { get; set; } = string.Empty;

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        EventId != Guid.Empty &&
        Sequence > 0 &&
        RequestId >= 0 &&
        Difficulty is >= 1 and <= 5 &&
        MaxMistakes is >= 1 and <= 10 &&
        Mistakes is >= 0 and <= 10 &&
        TimeLimitSeconds is >= 10 and <= 180 &&
        RemainingMilliseconds is >= 0 and <= 180_000 &&
        TurnPercent is >= 0 and <= 100 &&
        Hint is { Length: <= 32 } &&
        ErrorCode is { Length: <= 64 };
}
