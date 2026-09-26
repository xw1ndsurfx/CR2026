using Intersect.Framework.Core.MiniGames.Roulette;
using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public enum RouletteRequestKind
{
    Refresh = 0,
    Spin = 1,
    Leave = 2,
}

[MessagePackObject]
public sealed partial class RouletteTableState
{
    [Key(0)] public long SpinId { get; set; }
    [Key(1)] public long Revision { get; set; }
    [Key(2)] public long Balance { get; set; }
    [Key(3)] public long Bank { get; set; }
    [Key(4)] public long MinimumBet { get; set; }
    [Key(5)] public long MaximumBet { get; set; }
    [Key(6)] public int LastResult { get; set; } = -1;
    [Key(7)] public RouletteBetType LastBetType { get; set; }
    [Key(8)] public int LastBetNumber { get; set; } = -1;
    [Key(9)] public long LastWager { get; set; }
    [Key(10)] public long LastNet { get; set; }
    [Key(11)] public int[] History { get; set; } = [];
    [Key(12)] public Guid CurrencyItemId { get; set; }
    [Key(13)] public long Experience { get; set; }
    [Key(14)] public long Wins { get; set; }
    [Key(15)] public bool Pending { get; set; }

    public bool HasValidShape() =>
        SpinId >= 0 &&
        Revision >= 0 &&
        Balance is >= 0 and <= RouletteRules.MaximumBalance &&
        Bank is >= 0 and <= RouletteRules.MaximumBalance &&
        MinimumBet >= 1 &&
        MaximumBet >= MinimumBet &&
        MaximumBet <= 1_000_000_000 &&
        LastResult is >= -1 and <= RouletteRules.MaximumNumber &&
        LastBetType is >= RouletteBetType.Straight and <= RouletteBetType.Dozen3 &&
        LastBetNumber is >= -1 and <= RouletteRules.MaximumNumber &&
        LastWager is >= 0 and <= 1_000_000_000 &&
        LastNet is >= -1_000_000_000_000L and <= 1_000_000_000_000L &&
        History is { Length: <= 12 } &&
        History.All(RouletteRules.IsValidNumber) &&
        Experience is >= 0 and <= MiniGameProgression.MaximumExperience &&
        Wins >= 0;
}
