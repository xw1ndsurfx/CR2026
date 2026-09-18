using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Client;

/// <summary>Intent only. Identity, table rules, chip balances and time are server-owned.</summary>
[MessagePackObject]
public sealed partial class PokerRequestPacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId { get; set; }
    [Key(1)] public Guid ViewId { get; set; }
    [Key(2)] public long RequestId { get; set; }
    [Key(3)] public long HandId { get; set; }
    [Key(4)] public long Revision { get; set; }
    [Key(5)] public PokerRequestKind Kind { get; set; }
    // TOTAL wager on this street, not the amount to add. Ignored for other actions.
    [Key(6)] public long RaiseTo { get; set; }

    [IgnoreMember]
    public override bool IsValid => TableInstanceId != Guid.Empty && ViewId != Guid.Empty &&
        RequestId > 0 && HandId >= 0 && Revision >= 0 &&
        Kind is >= PokerRequestKind.Refresh and <= PokerRequestKind.Leave &&
        (Kind != PokerRequestKind.RaiseTo || RaiseTo > 0);
}
