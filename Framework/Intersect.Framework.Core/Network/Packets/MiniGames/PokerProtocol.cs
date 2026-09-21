using Intersect.Framework.Core.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public enum PokerStage { Waiting = 0, PreFlop = 1, Flop = 2, Turn = 3, River = 4, Finished = 5 }
public enum PokerRequestKind { Refresh = 0, StartHand = 1, Fold = 2, Check = 3, Call = 4, RaiseTo = 5, Leave = 6, SelectCardBack = 7 }

[MessagePackObject]
public sealed partial class PokerPlayerState
{
    [Key(0)] public int Seat { get; set; }
    [Key(1)] public Guid PlayerId { get; set; }
    [Key(2)] public string Name { get; set; } = string.Empty;
    [Key(3)] public long Chips { get; set; }
    [Key(4)] public long StreetBet { get; set; }
    [Key(5)] public bool InHand { get; set; }
    [Key(6)] public bool Folded { get; set; }
    [Key(7)] public bool AllIn { get; set; }
    [Key(8)] public bool Leaving { get; set; }
    [Key(9)] public int[] RevealedCards { get; set; } = [];
    [Key(10)] public int CardBackId { get; set; }
    [Key(11)] public int SelectedCardBackId { get; set; }
}

[MessagePackObject]
public sealed partial class PokerAwardState
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public long Chips { get; set; }
    [Key(2)] public bool IsRefund { get; set; }
}

[MessagePackObject]
public sealed partial class PokerDecisionState
{
    [Key(0)] public long Sequence { get; set; }
    [Key(1)] public Guid PlayerId { get; set; }
    [Key(2)] public string Name { get; set; } = string.Empty;
    [Key(3)] public string Action { get; set; } = string.Empty;
    [Key(4)] public long Amount { get; set; }
    [Key(5)] public bool Automatic { get; set; }
    [IgnoreMember] public bool IsValid => Sequence > 0 && PlayerId != Guid.Empty &&
        Name is { Length: >= 1 and <= 32 } && Amount >= 0 &&
        Action is "deal" or "check" or "call" or "raise" or "allin" or "fold" or "wins" or "leave";
}

[MessagePackObject]
public sealed partial class PokerEffectState
{
    [Key(0)] public PokerEffectKind Kind { get; set; }
    [Key(1)] public Guid AnimationId { get; set; }
    [Key(2)] public string Sound { get; set; } = string.Empty;
    [IgnoreMember] public bool IsValid => PokerEffects.IsValid(Kind) && Sound is { Length: <= 128 } &&
        Sound.All(c => !char.IsControl(c));
}

[MessagePackObject]
public sealed partial class PokerTableState
{
    [Key(0)] public long HandId { get; set; }
    [Key(1)] public long Revision { get; set; }
    [Key(2)] public PokerStage Stage { get; set; }
    [Key(3)] public int DealerSeat { get; set; } = -1;
    [Key(4)] public int ActingSeat { get; set; } = -1;
    [Key(5)] public long Pot { get; set; }
    [Key(6)] public long CurrentBet { get; set; }
    [Key(7)] public long ToCall { get; set; }
    [Key(8)] public long MinimumRaiseTo { get; set; }
    [Key(9)] public long MaximumRaiseTo { get; set; }
    [Key(10)] public bool CanRaise { get; set; }
    [Key(11)] public long DeadlineUnixMs { get; set; }
    [Key(12)] public int[] Board { get; set; } = [];
    [Key(13)] public int[] MyCards { get; set; } = [];
    [Key(14)] public PokerPlayerState[] Seats { get; set; } = [];
    [Key(15)] public PokerAwardState[] Payouts { get; set; } = [];
    [Key(16)] public Guid[] NpcIds { get; set; } = [];
    [Key(17)] public Guid DealerNpcId { get; set; }
    [Key(18)] public bool AutoStart { get; set; }
    [Key(19)] public Guid DealAnimationId { get; set; }
    [Key(20)] public long NetWin { get; set; }
    [Key(21)] public Guid VictoryAnimationId { get; set; }
    // Private progression belongs to the packet recipient; the feed contains public actions only.
    [Key(22)] public long Experience { get; set; }
    [Key(23)] public long Wins { get; set; }
    [Key(24)] public bool ProgressPending { get; set; }
    [Key(25)] public PokerDecisionState[] Decisions { get; set; } = [];
    [Key(28)] public PokerEffectState[] Effects { get; set; } = [];

    public bool HasValidShape() => HandId >= 0 && Revision >= 0 &&
        Stage is >= PokerStage.Waiting and <= PokerStage.Finished &&
        DealerSeat is >= -1 and < 6 && ActingSeat is >= -1 and < 6 &&
        Pot >= 0 && CurrentBet >= 0 && ToCall >= 0 && MinimumRaiseTo >= 0 && MaximumRaiseTo >= 0 &&
        NetWin >= 0 && (NetWin == 0 || Stage == PokerStage.Finished) &&
        (VictoryAnimationId == Guid.Empty || NetWin > 0) &&
        Experience is >= 0 and <= MiniGameProgression.MaximumExperience && Wins >= 0 &&
        ValidCards(Board, 5) && ValidCards(MyCards, 2) && Seats is { Length: >= 1 and <= 6 } &&
        Seats.All(s => s != null && s.Seat is >= 0 and < 6 && s.PlayerId != Guid.Empty &&
            s.Name is { Length: >= 1 and <= 32 } && s.Chips >= 0 && s.StreetBet >= 0 && ValidCards(s.RevealedCards, 2) &&
            MiniGameProgression.IsBack(s.CardBackId) && MiniGameProgression.IsBack(s.SelectedCardBackId)) &&
        Seats.Select(s => s.Seat).Distinct().Count() == Seats.Length &&
        Seats.Select(s => s.PlayerId).Distinct().Count() == Seats.Length &&
        Payouts is { Length: <= 36 } && Payouts.All(p => p != null && p.PlayerId != Guid.Empty && p.Chips >= 0) &&
        NpcIds is { Length: <= 5 } && NpcIds.Distinct().Count() == NpcIds.Length &&
        NpcIds.All(id => Seats.Any(s => s.PlayerId == id)) &&
        (DealerNpcId == Guid.Empty || NpcIds.Contains(DealerNpcId)) &&
        Decisions is { Length: <= 12 } && Decisions.All(d => d != null && d.IsValid) &&
        Decisions.Select(d => d.Sequence).Distinct().Count() == Decisions.Length &&
        Effects is { Length: <= PokerEffects.Count } && Effects.All(e => e != null && e.IsValid) &&
        Effects.Select(e => e.Kind).Distinct().Count() == Effects.Length;

    private static bool ValidCards(int[]? cards, int maximum) => cards != null &&
        cards.Length <= maximum && cards.All(c => c is >= 0 and < 52) && cards.Distinct().Count() == cards.Length;
}
