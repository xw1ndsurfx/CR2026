using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using MessagePack;
namespace Intersect.Network.Packets.MiniGames;
public enum DiceRequestKind { Refresh = 0, Roll = 1, Leave = 2 }
[MessagePackObject]
public sealed partial class DiceTableState
{
    [Key(0)] public long Revision { get; set; }
    [Key(1)] public long Balance { get; set; }
    [Key(2)] public long Bank { get; set; }
    [Key(3)] public long MinimumBet { get; set; }
    [Key(4)] public long MaximumBet { get; set; }
    [Key(5)] public long Step { get; set; }
    [Key(6)] public int OutsideReturn { get; set; }
    [Key(7)] public int SevenReturn { get; set; }
    [Key(8)] public Guid RoundId { get; set; }
    [Key(9)] public int First { get; set; }
    [Key(10)] public int Second { get; set; }
    [Key(11)] public DiceBet LastBet { get; set; }
    [Key(12)] public long Wager { get; set; }
    [Key(13)] public long Return { get; set; }
    [Key(14)] public long Experience { get; set; }
    [Key(15)] public long ExperienceAwarded { get; set; }
    [Key(16)] public Guid CurrencyItemId { get; set; }
    [Key(17)] public DiceParticipant[] Participants { get; set; } = [];
    public DiceRules Rules() => new(MinimumBet, MaximumBet, Step, OutsideReturn, SevenReturn);
    public bool HasValidShape() => Participants is { Length: <= 6 } && Participants.All(p => p != null && p.IsValid()) &&
        Participants.Select(p => p.PlayerId).Distinct().Count() == Participants.Length && Revision >= 0 && Balance is >= 0 and <= 1_000_000_000_000 &&
        Bank is >= 0 and <= 1_000_000_000_000 && Rules().IsValid && CurrencyItemId != Guid.Empty &&
        Experience is >= 0 and <= MiniGameProgression.MaximumExperience &&
        ExperienceAwarded is >= 0 and <= MiniGameProgression.MaximumExperience && Enum.IsDefined(LastBet) &&
        (RoundId == Guid.Empty ? First == 0 && Second == 0 && Wager == 0 && Return == 0 :
            DiceRules.IsFace(First) && DiceRules.IsFace(Second) && Wager is >= 1 and <= 20_000_000 &&
            Return is >= 0 and <= 1_000_000_000);
}

[MessagePackObject]
public sealed partial class DiceParticipant
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public Guid RoundId { get; set; }
    [Key(3)] public int First { get; set; }
    [Key(4)] public int Second { get; set; }
    [Key(5)] public DiceBet Bet { get; set; }
    [Key(6)] public long Wager { get; set; }
    [Key(7)] public long Return { get; set; }
    public bool IsValid() => PlayerId != Guid.Empty && Name is { Length: >= 1 and <= 128 } && Enum.IsDefined(Bet) &&
        (RoundId == Guid.Empty ? First == 0 && Second == 0 && Wager == 0 && Return == 0 :
            DiceRules.IsFace(First) && DiceRules.IsFace(Second) && Wager is >= 1 and <= 20_000_000 && Return is >= 0 and <= 1_000_000_000);
}
