using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed partial class RouletteStatePacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId { get; set; }
    [Key(1)] public Guid ViewId { get; set; }
    [Key(2)] public Guid PlayerId { get; set; }
    [Key(3)] public long Sequence { get; set; }
    [Key(4)] public long RequestId { get; set; }
    [Key(5)] public bool Closed { get; set; }
    [Key(6)] public string ErrorCode { get; set; } = string.Empty;
    [Key(7)] public string TableName { get; set; } = string.Empty;
    [Key(8)] public RouletteTableState? State { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        TableInstanceId != Guid.Empty &&
        ViewId != Guid.Empty &&
        PlayerId != Guid.Empty &&
        Sequence > 0 &&
        RequestId >= 0 &&
        ErrorCode is { Length: <= 160 } &&
        TableName is { Length: >= 1 and <= 64 } &&
        (Closed ? State == null : State is { } state && state.HasValidShape());
}
