using System;
using System.Collections.Generic;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

public enum PokerPhase { Waiting, PreFlop, Flop, Turn, River, Finished }
public enum PokerAction { Fold, Check, Call, RaiseTo }
public enum PokerError
{
    None, InvalidPlayer, AlreadySeated, TableFull, NotSeated, HandInProgress,
    NotEnoughPlayers, NoActiveHand, StaleState, NotYourTurn, IllegalAction, InvalidAmount,
}

public sealed record PokerRules(
    int MaxPlayers = 6, long StartingChips = 1000, long SmallBlind = 5,
    long BigBlind = 10, int TurnSeconds = 30);

public sealed record PokerSeatView(
    int Seat, Guid PlayerId, string Name, long Chips, long StreetBet, long Contribution,
    bool InHand, bool Folded, bool AllIn, bool Leaving, int[] RevealedCards);

public sealed record PokerPayout(Guid PlayerId, long Chips, bool IsRefund);

/// <summary>Detached, recipient-specific data. Never serialize the PokerTable object itself.</summary>
public sealed record PokerSnapshot(
    long HandId, long Revision, PokerPhase Phase, int DealerSeat, int ActingSeat,
    long Pot, long CurrentBet, long ToCall, long MinimumRaiseTo, long MaximumRaiseTo,
    bool CanRaise, DateTimeOffset Deadline, int[] Board, int[] MyCards,
    PokerSeatView[] Seats, PokerPayout[] Payouts);

/// <summary>
/// Server-owned no-limit hold'em state machine, using volatile test chips only.
/// All mutations and snapshots are serialized by one lock. No callbacks run under the lock.
/// The integration layer must derive player IDs from authenticated sessions, broadcast one
/// snapshot per recipient, call Tick regularly, and scope tables by map instance and table ID.
/// </summary>
public sealed class PokerTable
{
    private sealed class Seat
    {
        public int Index;
        public Guid Id;
        public string Name = "";
        public long Chips;
        public long Street;
        public long Contribution;
        public bool InHand;
        public bool Folded;
        public bool Leaving;
        public long LastActedBet = -1;
        public int[] Cards = Array.Empty<int>();
    }

    private readonly object _gate = new();
    private readonly PokerRules _rules;
    private readonly Func<int[]> _deckFactory;
    private readonly List<Seat> _seats = new();
    private readonly List<int> _board = new();
    private readonly HashSet<int> _pending = new();
    private readonly List<PokerPayout> _payouts = new();
    private int[] _deck = Array.Empty<int>();
    private int _draw;
    private int _dealer = -1;
    private int _acting = -1;
    private long _handId;
    private long _revision;
    private long _currentBet;
    private long _lastFullRaise;
    private PokerPhase _phase;
    private bool _showdown;
    private DateTimeOffset _deadline;

    public PokerTable(PokerRules rules) : this(rules, PokerCards.ShuffleDeck) { }

    // Deterministic decks are an internal test seam, not a client-controlled option.
    internal PokerTable(PokerRules rules, Func<int[]> deckFactory)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(deckFactory);
        if (rules.MaxPlayers < 2 || rules.MaxPlayers > 6 || rules.SmallBlind < 1 ||
            rules.BigBlind < rules.SmallBlind || rules.BigBlind > 1_000_000_000 ||
            rules.StartingChips < rules.BigBlind || rules.StartingChips > 1_000_000_000 ||
            rules.TurnSeconds < 5 || rules.TurnSeconds > 300)
            throw new ArgumentOutOfRangeException(nameof(rules));
        _rules = rules;
        _deckFactory = deckFactory;
    }

    public PokerError Join(Guid playerId, string name) => Join(playerId, name, _rules.StartingChips);

    internal PokerError Join(Guid playerId, string name, long chips)
    {
        lock (_gate)
        {
            if (playerId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 32)
                return PokerError.InvalidPlayer;
            if (chips < 1 || chips > 1_000_000_000) return PokerError.InvalidAmount;
            if (_seats.Any(s => s.Id == playerId)) return PokerError.AlreadySeated;
            if (_seats.Count >= _rules.MaxPlayers) return PokerError.TableFull;
            var index = Enumerable.Range(0, _rules.MaxPlayers).First(i => _seats.All(s => s.Index != i));
            _seats.Add(new Seat { Index = index, Id = playerId, Name = name, Chips = chips });
            ++_revision;
            return PokerError.None;
        }
    }

    public PokerError StartHand(Guid requester, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_seats.Any(s => s.Id == requester && !s.Leaving && s.Chips > 0)) return PokerError.NotSeated;
            if (IsPlaying) return PokerError.HandInProgress;
            var eligible = _seats.Where(s => !s.Leaving && s.Chips > 0).ToArray();
            if (eligible.Length < 2) return PokerError.NotEnoughPlayers;
            // Validate before changing any state, even when using the internal test seam.
            var deck = _deckFactory();
            if (deck == null || deck.Length != 52 || deck.Any(c => c < 0 || c >= 52) || deck.Distinct().Count() != 52)
                throw new InvalidOperationException("The deck must contain 52 distinct card IDs.");
            _deck = (int[])deck.Clone();
            _draw = 0;
            _seats.RemoveAll(s => s.Leaving);
            foreach (var seat in _seats)
            {
                seat.InHand = seat.Chips > 0;
                seat.Folded = false;
                seat.Street = seat.Contribution = 0;
                seat.LastActedBet = -1;
                seat.Cards = Array.Empty<int>();
            }
            _dealer = Next(_dealer, eligible).Index;
            var small = eligible.Length == 2 ? At(_dealer) : Next(_dealer, eligible);
            var big = Next(small.Index, eligible);
            // Deal one card at a time clockwise, starting left of the button.
            var order = Clockwise(_dealer, eligible).ToArray();
            foreach (var seat in order) seat.Cards = new int[2];
            for (var round = 0; round < 2; ++round)
                foreach (var seat in order) seat.Cards[round] = _deck[_draw++];
            PutChips(small, Math.Min(_rules.SmallBlind, small.Chips));
            PutChips(big, Math.Min(_rules.BigBlind, big.Chips));
            _board.Clear();
            _payouts.Clear();
            _showdown = false;
            _phase = PokerPhase.PreFlop;
            _currentBet = _lastFullRaise = _rules.BigBlind;
            _pending.Clear();
            foreach (var seat in Active) _pending.Add(seat.Index); // Blinds do not count as voluntary action.
            ++_handId;
            Progress(big.Index, now);
            ++_revision;
            return PokerError.None;
        }
    }

    public PokerError Act(Guid playerId, long handId, long revision, PokerAction action, long raiseTo, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!IsPlaying) return PokerError.NoActiveHand;
            if (handId != _handId || revision != _revision) return PokerError.StaleState;
            var seat = _seats.FirstOrDefault(s => s.Id == playerId);
            if (seat == null) return PokerError.NotSeated;
            if (seat.Index != _acting || seat.Leaving) return PokerError.NotYourTurn;
            // The deadline is authoritative even if the server's scheduled Tick has not run yet.
            if (now >= _deadline)
            {
                AutoAct(seat, now);
                ++_revision;
                return PokerError.StaleState;
            }
            var error = Apply(seat, action, raiseTo);
            if (error != PokerError.None) return error;
            Progress(seat.Index, now);
            ++_revision;
            return PokerError.None;
        }
    }

    /// <summary>Call on a server timer, including when no clients send actions.</summary>
    public bool Tick(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!IsPlaying || _acting < 0 || now < _deadline) return false;
            AutoAct(At(_acting), now);
            ++_revision;
            return true;
        }
    }

    /// <summary>
    /// During a hand a departing seat auto-checks/folds in turn; all-in eligibility is retained.
    /// It is never replaced during that hand. The adapter must keep its membership until settled.
    /// Chips are test-only and are discarded when a seat is finally removed.
    /// </summary>
    public PokerError Leave(Guid playerId, DateTimeOffset now)
    {
        lock (_gate)
        {
            var seat = _seats.FirstOrDefault(s => s.Id == playerId);
            if (seat == null) return PokerError.NotSeated;
            if (!IsPlaying || !seat.InHand) _seats.Remove(seat);
            else
            {
                seat.Leaving = true;
                if (seat.Index == _acting) AutoAct(seat, now);
            }
            ++_revision;
            return PokerError.None;
        }
    }

    public PokerSnapshot Snapshot(Guid viewer)
    {
        lock (_gate)
        {
            var me = _seats.FirstOrDefault(s => s.Id == viewer);
            if (me == null) throw new InvalidOperationException("Only a seated recipient can request a snapshot.");
            var myTurn = IsPlaying && _acting == me.Index && !me.Leaving;
            return new PokerSnapshot(_handId, _revision, _phase, _dealer, _acting,
                _seats.Sum(s => s.Contribution), _currentBet,
                myTurn ? Math.Min(me.Chips, Math.Max(0, _currentBet - me.Street)) : 0,
                MinimumRaise, me.Street + me.Chips, myTurn && MayRaise(me), _deadline,
                _board.ToArray(), (int[])me.Cards.Clone(),
                _seats.OrderBy(s => s.Index).Select(s => new PokerSeatView(s.Index, s.Id, s.Name,
                    s.Chips, s.Street, s.Contribution, s.InHand, s.Folded,
                    IsPlaying && s.InHand && !s.Folded && s.Chips == 0, s.Leaving,
                    _showdown && s.InHand && !s.Folded ? (int[])s.Cards.Clone() : Array.Empty<int>())).ToArray(),
                _payouts.ToArray());
        }
    }

    private bool IsPlaying => _phase >= PokerPhase.PreFlop && _phase <= PokerPhase.River;
    private IEnumerable<Seat> Live => _seats.Where(s => s.InHand && !s.Folded);
    private IEnumerable<Seat> Active => Live.Where(s => s.Chips > 0);
    private Seat At(int index) => _seats.Single(s => s.Index == index);
    private IEnumerable<Seat> Clockwise(int after, IEnumerable<Seat> seats) =>
        seats.OrderBy(s => (s.Index - after - 1 + _rules.MaxPlayers) % _rules.MaxPlayers);
    private Seat Next(int after, IEnumerable<Seat> seats) => Clockwise(after, seats).First();
    private long MinimumRaise => _currentBet + _lastFullRaise;

    private bool MayRaise(Seat seat) => Active.Any(s => s.Index != seat.Index) &&
        seat.Chips + seat.Street > _currentBet &&
        (seat.LastActedBet < 0 || _currentBet - seat.LastActedBet >= _lastFullRaise);

    private static void PutChips(Seat seat, long amount)
    {
        seat.Chips -= amount;
        seat.Street += amount;
        seat.Contribution += amount;
    }

    private PokerError Apply(Seat seat, PokerAction action, long raiseTo)
    {
        var owed = Math.Max(0, _currentBet - seat.Street);
        switch (action)
        {
            case PokerAction.Fold:
                seat.Folded = true;
                break;
            case PokerAction.Check:
                if (owed != 0) return PokerError.IllegalAction;
                break;
            case PokerAction.Call:
                if (owed == 0) return PokerError.IllegalAction;
                PutChips(seat, Math.Min(seat.Chips, owed));
                break;
            case PokerAction.RaiseTo:
                if (!MayRaise(seat)) return PokerError.IllegalAction;
                var maximum = seat.Street + seat.Chips;
                if (raiseTo <= _currentBet || raiseTo > maximum || (raiseTo < MinimumRaise && raiseTo != maximum))
                    return PokerError.InvalidAmount;
                var increment = raiseTo - _currentBet;
                if (increment >= _lastFullRaise) _lastFullRaise = increment;
                PutChips(seat, raiseTo - seat.Street);
                _currentBet = raiseTo;
                foreach (var other in Active.Where(s => s.Index != seat.Index && s.Street < _currentBet))
                    _pending.Add(other.Index);
                break;
            default:
                return PokerError.IllegalAction;
        }
        seat.LastActedBet = _currentBet;
        _pending.Remove(seat.Index);
        return PokerError.None;
    }

    private void AutoAct(Seat seat, DateTimeOffset now)
    {
        Apply(seat, seat.Street >= _currentBet ? PokerAction.Check : PokerAction.Fold, 0);
        Progress(seat.Index, now);
    }

    private void Progress(int after, DateTimeOffset now)
    {
        while (IsPlaying)
        {
            var live = Live.ToArray();
            if (live.Length == 1) { Settle(false); return; }
            var active = Active.ToArray();
            _pending.RemoveWhere(i => active.All(s => s.Index != i));
            if (active.Length <= 1)
            {
                // No dry-side-pot betting. A lone player can only call an actual opponent's wager.
                var owes = active.Length == 1 && active[0].Street < live.Where(s => s != active[0]).Max(s => s.Street);
                if (!owes)
                {
                    while (_phase < PokerPhase.River) DealStreet();
                    Settle(true);
                    return;
                }
                _currentBet = live.Max(s => s.Street);
                _pending.Add(active[0].Index);
            }
            if (_pending.Count == 0)
            {
                if (_phase == PokerPhase.River) { Settle(true); return; }
                DealStreet();
                after = _dealer;
                continue;
            }
            var next = Next(after, active.Where(s => _pending.Contains(s.Index)));
            if (next.Leaving)
            {
                Apply(next, next.Street >= _currentBet ? PokerAction.Check : PokerAction.Fold, 0);
                after = next.Index;
                continue;
            }
            _acting = next.Index;
            _deadline = now.AddSeconds(_rules.TurnSeconds);
            return;
        }
    }

    private void DealStreet()
    {
        ++_draw; // Burn a card before each board street.
        var count = _phase == PokerPhase.PreFlop ? 3 : 1;
        for (var i = 0; i < count; ++i) _board.Add(_deck[_draw++]);
        _phase = (PokerPhase)((int)_phase + 1);
        _currentBet = 0;
        _lastFullRaise = _rules.BigBlind;
        _pending.Clear();
        foreach (var seat in _seats)
        {
            seat.Street = 0;
            seat.LastActedBet = -1;
        }
        foreach (var seat in Active) _pending.Add(seat.Index);
    }

    private void Settle(bool showdown)
    {
        _showdown = showdown;
        var scores = Live.ToDictionary(s => s.Index,
            s => showdown ? PokerCards.Evaluate(s.Cards.Concat(_board)) : 0L);
        var levels = _seats.Select(s => s.Contribution).Where(c => c > 0).Distinct().OrderBy(c => c).ToArray();
        long previous = 0;
        foreach (var level in levels)
        {
            var contributors = _seats.Where(s => s.Contribution >= level).ToArray();
            var amount = (level - previous) * contributors.Length;
            previous = level;
            if (contributors.Length == 1)
            {
                Award(contributors[0], amount, true); // Return unmatched chips, including to a folded seat.
                continue;
            }
            var eligible = contributors.Where(s => s.InHand && !s.Folded).ToArray();
            if (eligible.Length == 0) throw new InvalidOperationException("A contested pot has no eligible player.");
            var best = eligible.Max(s => scores[s.Index]);
            var winners = Clockwise(_dealer, eligible.Where(s => scores[s.Index] == best)).ToArray();
            for (var i = 0; i < winners.Length; ++i)
                Award(winners[i], amount / winners.Length + (i < amount % winners.Length ? 1 : 0), false);
        }
        foreach (var seat in _seats) seat.Street = seat.Contribution = 0;
        _phase = PokerPhase.Finished;
        _acting = -1;
        _currentBet = 0;
        _pending.Clear();
        _deadline = default;
    }

    private void Award(Seat seat, long chips, bool refund)
    {
        seat.Chips += chips;
        _payouts.Add(new PokerPayout(seat.Id, chips, refund));
    }
}
