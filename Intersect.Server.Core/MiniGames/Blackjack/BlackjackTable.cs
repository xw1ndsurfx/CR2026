#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames.Blackjack;

namespace Intersect.Server.MiniGames.Blackjack;

public sealed record BlackjackHandSnapshot(int[] Cards, long Bet, int Total, bool Soft,
    bool Natural, bool Done, bool SplitAces, BlackjackOutcome Outcome, long Net);
public sealed record BlackjackSeatSnapshot(int Seat, Guid PlayerId, string Name, bool Npc, long Chips,
    bool Leaving, bool InRound, BlackjackHandSnapshot[] Hands, string LastAction);
public sealed record BlackjackSnapshot(long HandId, long Revision, BlackjackStage Stage, long Bank,
    int ActingSeat, int ActingHand, DateTimeOffset Deadline, int[] DealerCards, bool DealerHoleHidden,
    int DealerTotal, bool DealerSoft, BlackjackSeatSnapshot[] Seats,
    bool CanBet, long MaximumBet, bool CanHit, bool CanStand, bool CanDouble, bool CanSplit);

/// <summary>
/// Server-only state machine. The enclosing session serializes calls. No inventory/network callbacks.
/// All player hands are face-up; the dealer's hole card and its derived total never leave Snapshot
/// until the dealer's turn (or an initial dealer blackjack). Reserve 4x each bet before admitting it,
/// covering a single split and doubles on both resulting hands without an unfunded house promise.
/// </summary>
public sealed class BlackjackTable
{
    private sealed class Hand
    {
        public List<int> Cards = new(); public long Bet; public bool Done, Split, SplitAces;
        public BlackjackOutcome Outcome; public long Net;
        public (int Total, bool Soft) Value => BlackjackValues.Count(Cards);
        public bool Natural => !Split && Cards.Count == 2 && Value.Total == 21;
    }
    private sealed class Seat
    {
        public int Index; public Guid Id; public string Name = ""; public bool Npc, Leaving, InRound;
        public long Chips, ReservedRisk; public List<Hand> Hands = new(); public string LastAction = "";
    }
    public BlackjackRules Rules { get; }
    public long HandId { get; private set; }
    public long Revision { get; private set; }
    public BlackjackStage Stage { get; private set; }
    public long Bank { get; private set; }
    public bool Busy => Stage is BlackjackStage.Betting or BlackjackStage.Players or BlackjackStage.Dealer;
    private readonly Func<int[]> _shuffle;
    private readonly List<Seat> _seats = new();
    private readonly List<int> _dealer = new();
    private int[] _shoe = Array.Empty<int>(); private int _draw;
    private int _actingSeat = -1, _actingHand = -1;
    private DateTimeOffset _deadline;
    public BlackjackTable(BlackjackRules rules, long bank) : this(rules, bank, Shuffle) { }
    internal BlackjackTable(BlackjackRules rules, long bank, Func<int[]> shuffle)
    {
        if (!rules.IsValid || bank < 0 || bank > BlackjackRules.MaximumBalance) throw new ArgumentOutOfRangeException(nameof(rules));
        Rules = rules; Bank = bank; _shuffle = shuffle;
    }
    public static int[] Shuffle()
    {
        var cards = Enumerable.Range(0, 52 * BlackjackRules.DeckCount).Select(n => n % 52).ToArray();
        for (var i = cards.Length - 1; i > 0; --i)
        { var j = RandomNumberGenerator.GetInt32(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); }
        return cards;
    }
    public BlackjackError Join(Guid id, string name, long chips, bool npc = false)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 32 || chips < 0 || chips > BlackjackRules.MaximumBalance)
            return BlackjackError.InvalidPlayer;
        if (_seats.Any(s => s.Id == id)) return BlackjackError.IllegalAction;
        if (_seats.Count >= Rules.MaxPlayers) return BlackjackError.Full;
        var index = Enumerable.Range(0, Rules.MaxPlayers).First(i => _seats.All(s => s.Index != i));
        _seats.Add(new Seat { Index = index, Id = id, Name = name, Chips = chips, Npc = npc }); ++Revision;
        return BlackjackError.None;
    }
    public BlackjackError Remove(Guid id)
    {
        if (Busy) return BlackjackError.Busy;
        var seat = _seats.FirstOrDefault(s => s.Id == id);
        if (seat == null) return BlackjackError.NotSeated;
        _seats.Remove(seat); ++Revision; return BlackjackError.None;
    }
    public BlackjackError Begin(long revision, DateTimeOffset now)
    {
        if (revision != Revision) return BlackjackError.StaleState;
        if (Busy) return BlackjackError.Busy;
        if (!_seats.Any(s => !s.Npc && !s.Leaving && s.Chips >= Rules.MinimumBet)) return BlackjackError.NoHumanBet;
        if (Bank < Rules.MinimumBet * 4) return BlackjackError.BankTooLow;
        var shoe = _shuffle();
        if (shoe.Length != 52 * BlackjackRules.DeckCount || shoe.Any(c => c is < 0 or >= 52) ||
            Enumerable.Range(0, 52).Any(c => shoe.Count(n => n == c) != BlackjackRules.DeckCount))
            throw new InvalidOperationException("Invalid blackjack shoe.");
        _shoe = (int[])shoe.Clone(); _draw = 0; _dealer.Clear(); ++HandId;
        _actingSeat = _actingHand = -1;
        foreach (var seat in _seats)
        {
            seat.Hands.Clear(); seat.ReservedRisk = 0; seat.LastAction = "";
            seat.InRound = !seat.Leaving && seat.Chips >= Rules.MinimumBet;
        }
        Stage = BlackjackStage.Betting; _deadline = now.AddSeconds(Rules.TurnSeconds); ++Revision;
        return BlackjackError.None;
    }
    private long Maximum(Seat seat) => Math.Max(0, Math.Min(Rules.MaximumBet,
        Math.Min(seat.Chips, (Bank - _seats.Sum(s => s.ReservedRisk)) / 4))) / 2 * 2;
    public BlackjackError Bet(Guid id, long hand, long revision, long bet, DateTimeOffset now)
    {
        if (hand != HandId || revision != Revision) return BlackjackError.StaleState;
        var seat = _seats.FirstOrDefault(s => s.Id == id);
        if (seat == null) return BlackjackError.NotSeated;
        if (Stage != BlackjackStage.Betting || !seat.InRound || seat.Leaving || seat.Hands.Count != 0) return BlackjackError.IllegalAction;
        if (bet < Rules.MinimumBet || bet > Rules.MaximumBet || bet % 2 != 0) return BlackjackError.InvalidBet;
        if (bet > seat.Chips) return BlackjackError.InsufficientChips;
        if (bet > Maximum(seat)) return BlackjackError.BankTooLow;
        seat.Chips -= bet; seat.Hands.Add(new Hand { Bet = bet }); seat.ReservedRisk = checked(bet * 4);
        seat.LastAction = "Bet " + bet; ++Revision;
        if (_seats.Where(s => s.InRound && !s.Leaving).All(s => s.Hands.Count > 0)) Deal(now);
        return BlackjackError.None;
    }
    private int Draw() => _draw < _shoe.Length ? _shoe[_draw++] : throw new InvalidOperationException("Shoe exhausted.");
    private void Deal(DateTimeOffset now)
    {
        if (!_seats.Any(s => !s.Npc && !s.Leaving && s.Hands.Count > 0))
        {
            foreach (var seat in _seats)
            { seat.Chips += seat.Hands.Sum(h => h.Bet); seat.Hands.Clear(); seat.InRound = false; seat.ReservedRisk = 0; }
            Stage = BlackjackStage.Waiting; _deadline = default; ++Revision; return;
        }
        var playing = _seats.Where(s => s.Hands.Count > 0 && !s.Leaving).OrderBy(s => s.Index).ToArray();
        foreach (var seat in playing) seat.Hands[0].Cards.Add(Draw());
        _dealer.Add(Draw());
        foreach (var seat in playing) seat.Hands[0].Cards.Add(Draw());
        _dealer.Add(Draw());
        foreach (var seat in playing)
        { seat.Hands[0].Done = seat.Hands[0].Natural; seat.LastAction = seat.Hands[0].Natural ? "Blackjack" : "Dealt"; }
        Stage = BlackjackStage.Players; ++Revision;
        // American hole-card/peek: no extra player wagers before a dealer natural is settled.
        if (BlackjackValues.Count(_dealer).Total == 21) { Finish(); return; }
        Advance(now);
    }
    public BlackjackError Act(Guid id, long hand, long revision, BlackjackAction action, DateTimeOffset now)
    {
        if (hand != HandId || revision != Revision) return BlackjackError.StaleState;
        var seat = _seats.FirstOrDefault(s => s.Id == id);
        if (seat == null) return BlackjackError.NotSeated;
        if (Stage != BlackjackStage.Players || seat.Index != _actingSeat || seat.Leaving) return BlackjackError.NotYourTurn;
        var current = seat.Hands[_actingHand];
        if (current.Done) return BlackjackError.IllegalAction;
        switch (action)
        {
            case BlackjackAction.Hit:
                current.Cards.Add(Draw()); current.Done = current.Value.Total >= 21;
                seat.LastAction = current.Value.Total > 21 ? "Bust" : "Hit"; break;
            case BlackjackAction.Stand:
                current.Done = true; seat.LastAction = "Stand"; break;
            case BlackjackAction.Double:
                if (!CanDouble(seat, current)) return BlackjackError.IllegalAction;
                seat.Chips -= current.Bet; current.Bet *= 2; current.Cards.Add(Draw()); current.Done = true;
                seat.LastAction = current.Value.Total > 21 ? "Double / Bust" : "Double"; break;
            case BlackjackAction.Split:
                if (!CanSplit(seat, current)) return BlackjackError.IllegalAction;
                seat.Chips -= current.Bet;
                var card = current.Cards[1]; current.Cards.RemoveAt(1);
                current.Split = true; current.SplitAces = card % 13 == 12;
                var other = new Hand { Bet = current.Bet, Split = true, SplitAces = current.SplitAces };
                other.Cards.Add(card); current.Cards.Add(Draw()); other.Cards.Add(Draw());
                current.Done = current.SplitAces || current.Value.Total == 21;
                other.Done = other.SplitAces || other.Value.Total == 21; seat.Hands.Add(other);
                seat.LastAction = "Split"; break;
            default: return BlackjackError.IllegalAction;
        }
        ++Revision; _deadline = now.AddSeconds(Rules.TurnSeconds); Advance(now); return BlackjackError.None;
    }
    private static bool CanDouble(Seat seat, Hand hand) => !hand.Done && !hand.SplitAces && hand.Cards.Count == 2 && seat.Chips >= hand.Bet;
    private static bool CanSplit(Seat seat, Hand hand) => !hand.Done && !hand.Split && seat.Hands.Count == 1 &&
        hand.Cards.Count == 2 && hand.Cards[0] % 13 == hand.Cards[1] % 13 && seat.Chips >= hand.Bet;
    private void Advance(DateTimeOffset now)
    {
        foreach (var seat in _seats.OrderBy(s => s.Index))
        for (var i = 0; i < seat.Hands.Count; ++i)
        {
            if (seat.Leaving) seat.Hands[i].Done = true;
            if (seat.Hands[i].Done) continue;
            if (_actingSeat != seat.Index || _actingHand != i) _deadline = now.AddSeconds(Rules.TurnSeconds);
            _actingSeat = seat.Index; _actingHand = i; return;
        }
        Stage = BlackjackStage.Dealer; _actingSeat = _actingHand = -1; _deadline = now.AddMilliseconds(900);
        ++Revision;
        if (!_seats.SelectMany(s => s.Hands).Any(h => h.Value.Total <= 21 && !h.Natural)) Finish();
    }
    public void Tick(DateTimeOffset now)
    {
        if (!Busy || now < _deadline) return;
        if (Stage == BlackjackStage.Betting) { Deal(now); return; }
        if (Stage == BlackjackStage.Players)
        {
            var seat = _seats.Single(s => s.Index == _actingSeat);
            Act(seat.Id, HandId, Revision, BlackjackAction.Stand, now); seat.LastAction = "Stand (timeout)"; return;
        }
        var (total, soft) = BlackjackValues.Count(_dealer);
        if (total < 17 || total == 17 && soft && Rules.HitSoft17)
        { _dealer.Add(Draw()); _deadline = now.AddMilliseconds(900); ++Revision; }
        else Finish();
    }
    private void Finish()
    {
        var dealer = BlackjackValues.Count(_dealer).Total; var dealerNatural = _dealer.Count == 2 && dealer == 21;
        foreach (var seat in _seats)
        foreach (var hand in seat.Hands)
        {
            var total = hand.Value.Total;
            var net = total > 21 ? -hand.Bet : dealerNatural ? hand.Natural ? 0 : -hand.Bet :
                hand.Natural ? hand.Bet * 3 / 2 : dealer > 21 || total > dealer ? hand.Bet : total == dealer ? 0 : -hand.Bet;
            hand.Net = net; hand.Done = true;
            hand.Outcome = net < 0 ? BlackjackOutcome.Lose : net == 0 ? BlackjackOutcome.Push :
                hand.Natural ? BlackjackOutcome.Blackjack : BlackjackOutcome.Win;
            seat.Chips = checked(seat.Chips + hand.Bet + net); Bank = checked(Bank - net);
            if (Bank < 0) throw new InvalidOperationException("Insufficient pre-reserved dealer exposure.");
        }
        foreach (var seat in _seats.Where(s => s.Hands.Count > 0))
        { var net = seat.Hands.Sum(h => h.Net); seat.LastAction = net > 0 ? "Wins +" + net : net < 0 ? "Loses " + (-net) : "Push"; }
        Stage = BlackjackStage.Finished; _deadline = default; _actingSeat = _actingHand = -1; ++Revision;
    }
    public void Leave(Guid id, DateTimeOffset now)
    {
        var seat = _seats.FirstOrDefault(s => s.Id == id); if (seat == null || seat.Leaving) return;
        seat.Leaving = true; seat.LastAction = "Leaving";
        if (Stage == BlackjackStage.Betting)
        { seat.Chips += seat.Hands.Sum(h => h.Bet); seat.Hands.Clear(); seat.InRound = false; seat.ReservedRisk = 0; }
        else foreach (var hand in seat.Hands) hand.Done = true;
        if (Stage == BlackjackStage.Players) Advance(now);
        ++Revision;
    }
    public BlackjackSnapshot Snapshot(Guid viewer)
    {
        var me = _seats.FirstOrDefault(s => s.Id == viewer);
        var hidden = Stage == BlackjackStage.Players;
        var cards = hidden ? _dealer.Take(1).ToArray() : _dealer.ToArray();
        var value = BlackjackValues.Count(cards);
        var acting = me != null && !me.Leaving && Stage == BlackjackStage.Players && _actingSeat == me.Index;
        var hand = acting ? me!.Hands[_actingHand] : null;
        return new(HandId, Revision, Stage, Bank, _actingSeat, _actingHand, _deadline, cards, hidden,
            value.Total, value.Soft, _seats.OrderBy(s => s.Index).Select(s => new BlackjackSeatSnapshot(s.Index, s.Id, s.Name,
                s.Npc, s.Chips, s.Leaving, s.InRound, s.Hands.Select(h => new BlackjackHandSnapshot(h.Cards.ToArray(), h.Bet,
                    h.Value.Total, h.Value.Soft, h.Natural, h.Done, h.SplitAces, h.Outcome, h.Net)).ToArray(), s.LastAction)).ToArray(),
            me != null && Stage == BlackjackStage.Betting && me.InRound && !me.Leaving && me.Hands.Count == 0 && Maximum(me) >= Rules.MinimumBet,
            me == null ? 0 : Maximum(me), acting, acting, acting && CanDouble(me!, hand!), acting && CanSplit(me!, hand!));
    }
}
