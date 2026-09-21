using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Server;

/// <summary>Recipient-specific, detached state. Also acknowledges requests and closes a view.</summary>
[MessagePackObject]
public sealed partial class PokerStatePacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId { get; set; }
    [Key(1)] public Guid ViewId { get; set; }
    [Key(2)] public Guid PlayerId { get; set; }
    [Key(3)] public long Sequence { get; set; }
    [Key(4)] public long RequestId { get; set; }
    [Key(5)] public bool Closed { get; set; }
    [Key(6)] public string ErrorCode { get; set; } = string.Empty;
    [Key(7)] public string TableName { get; set; } = string.Empty;
    [Key(8)] public long ServerUnixMs { get; set; }
    [Key(9)] public PokerTableState? State { get; set; }

    [IgnoreMember]
    public override bool IsValid => TableInstanceId != Guid.Empty && ViewId != Guid.Empty &&
        PlayerId != Guid.Empty && Sequence > 0 && RequestId >= 0 &&
        ErrorCode is { Length: <= 64 } && TableName is { Length: <= 64 } &&
        (Closed ? State == null : State != null && State.HasValidShape() && State.Seats.Any(s => s.PlayerId == PlayerId));
}
