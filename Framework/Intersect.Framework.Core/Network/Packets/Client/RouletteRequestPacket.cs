using Intersect.Framework.Core.MiniGames.Roulette;
using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public sealed partial class RouletteRequestPacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId { get; set; }
    [Key(1)] public Guid ViewId { get; set; }
    [Key(2)] public long RequestId { get; set; }
    [Key(3)] public long Revision { get; set; }
    [Key(4)] public RouletteRequestKind Kind { get; set; }
    [Key(5)] public RouletteBetType BetType { get; set; }
    [Key(6)] public int Number { get; set; } = -1;
    [Key(7)] public long Amount { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        TableInstanceId != Guid.Empty &&
        ViewId != Guid.Empty &&
        RequestId > 0 &&
        Revision >= 0 &&
        Kind is >= RouletteRequestKind.Refresh and <= RouletteRequestKind.Leave &&
        (Kind == RouletteRequestKind.Spin
            ? Amount is >= 1 and <= 1_000_000_000 && RouletteRules.IsValidBet(BetType, Number)
            : Amount == 0);
}
