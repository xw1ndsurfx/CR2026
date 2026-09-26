using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Server.MiniGames.Poker;

var tests = new (string Name, Action Run)[]
{
    ("All hand categories, kickers and seven-card selection", HandRanks),
    ("Deck validity and defensive card validation", CardsAndDeck),
    ("Heads-up blinds, big-blind option and hidden cards", HeadsUp),
    ("Fold winner and unmatched-chip refund", FoldAndRefund),
    ("Bet validation, out-of-turn and replay rejection", Validation),
    ("Joining mid-hand and snapshot defensive copies", LateJoin),
    ("Unequal all-ins and side pots", SidePots),
    ("Tied pot with odd chip", OddChip),
    ("Short all-in does not reopen a prior caller", ShortRaise),
    ("Cumulative short all-ins reopen betting", CumulativeRaise),
    ("Timeout advances without client input", Timeout),
    ("Expired action cannot beat the server deadline", ExpiredAction),
    ("Departing all-in player remains eligible", AllInLeave),
    ("Out-of-turn departure auto-acts when reached", Departure),
    ("Short blind does not create a dry side pot", ShortBlind),
    ("Repeated hands rotate the dealer", RepeatedHands),
    ("Deterministic simulated games conserve every chip", Simulations),
    ("Royal Kitchen stage rules are deterministic and bounded", CookingStages),
};
var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception error) { ++failures; Console.Error.WriteLine("FAIL " + test.Name + ": " + error); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} core tests passed");
// Run concurrent tests from Main, not a module/static initializer whose loader lock
// prevents worker threads from entering code in this assembly until initialization ends.
try { RegistrySmokeTests.RunAll(); }
catch (Exception error) { ++failures; Console.Error.WriteLine(error); }
Environment.ExitCode = failures == 0 ? 0 : 1;

static void CookingStages()
{
    Check(CookingStageRules.TargetTolerance(1) > CookingStageRules.TargetTolerance(5), "Difficulty tolerance");
    Check(CookingStageRules.TimingCursorPermille(0, 10_000, 3) == 0, "Timing cursor start");
    Check(CookingStageRules.TimingCursorPermille(10_000, 10_000, 3) is >= 0 and <= 1000, "Timing cursor bounds");
    Check(CookingStageRules.PrecisionScore(500, 500, 100) == 100, "Perfect precision");
    Check(CookingStageRules.MoveMeter(950, 200) == 1000, "Meter upper clamp");
    Check(CookingStageRules.MoveMeter(50, -200) == 0, "Meter lower clamp");
    Check(CookingStageRules.PlateScore(CookingActionInput.Secondary, 500) == 100, "Plate center");
    Check(CookingStageRules.PlateScore(CookingActionInput.Primary, 800) == 15, "Plate wrong side");
}

static DateTimeOffset Now() => DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
static Guid Id(int i) => new(i, 0, 0, new byte[8]);
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Ok(PokerError error) => Check(error == PokerError.None, "Unexpected error: " + error);
static void Throws(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new Exception("Expected an argument exception");
}
static int[] Cards(string cards) => cards.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c =>
{
    var rank = "23456789TJQKA".IndexOf(c[0]);
    var suit = "CDHS".IndexOf(c[1]);
    if (rank < 0 || suit < 0 || c.Length != 2) throw new Exception("Bad test card");
    return suit * 13 + rank;
}).ToArray();
static int[] Deck(string prefix = "")
{
    var cards = Cards(prefix);
    Check(cards.Distinct().Count() == cards.Length, "Duplicate in rigged test deck");
    return cards.Concat(Enumerable.Range(0, 52).Except(cards)).ToArray();
}
static PokerTable Table(int count, string deck = "", long[]? stacks = null)
{
    var table = new PokerTable(new PokerRules(), () => Deck(deck));
    for (var i = 1; i <= count; ++i) Ok(table.Join(Id(i), "Player " + i, stacks?[i - 1] ?? 1000));
    return table;
}
static PokerSnapshot View(PokerTable table, int viewer = 1) => table.Snapshot(Id(viewer));
static PokerError Act(PokerTable table, int player, PokerAction action, long amount = 0, DateTimeOffset? now = null)
{
    var view = View(table, player);
    return table.Act(Id(player), view.HandId, view.Revision, action, amount, now ?? Now());
}
static long Total(PokerSnapshot view) => view.Pot + view.Seats.Sum(s => s.Chips);
static void CallOrCheck(PokerTable table)
{
    var view = View(table);
    var actor = view.Seats.Single(s => s.Seat == view.ActingSeat).PlayerId;
    var own = table.Snapshot(actor);
    Ok(table.Act(actor, own.HandId, own.Revision, own.ToCall > 0 ? PokerAction.Call : PokerAction.Check, 0, Now()));
}
static void Finish(PokerTable table)
{
    var n = 0;
    while (View(table).Phase != PokerPhase.Finished && ++n < 200) CallOrCheck(table);
    Check(View(table).Phase == PokerPhase.Finished, "Hand did not terminate");
}

static void HandRanks()
{
    string[] hands = {
        "AC JD 9H 6S 3C", "AC AD JH 6S 3C", "AC AD JH JS 3C", "AC AD AH JS 3C",
        "2C 3D 4H 5S 6C", "AC JC 9C 6C 3C", "AC AD AH JS JC", "AC AD AH AS JC", "TC JC QC KC AC",
    };
    long previous = -1;
    for (var i = 0; i < hands.Length; ++i)
    {
        var score = PokerCards.Evaluate(Cards(hands[i]));
        Check((int)PokerCards.Category(score) == i && score > previous, "Category ordering");
        previous = score;
    }
    Check(PokerCards.Evaluate(Cards("AC 2D 3H 4S 5C")) < PokerCards.Evaluate(Cards("2C 3D 4H 5S 6C")), "Wheel");
    Check(PokerCards.Evaluate(Cards("AC AD KH 6S 3C")) > PokerCards.Evaluate(Cards("AC AD QH 6S 3C")), "Pair kicker");
    Check(PokerCards.Evaluate(Cards("AC AD AH KC KD KH 2S")) == PokerCards.Evaluate(Cards("AC AD AH KC KD")), "Two trips");
    Check(PokerCards.Evaluate(Cards("2D 3H TC JC QC KC AC")) == previous, "Best five of seven");
    Check(PokerCards.Evaluate(Cards("AC JD 9H 6S 3C")) == PokerCards.Evaluate(Cards("AS JH 9D 6C 3S")), "Suits must not break ties");
}
static void CardsAndDeck()
{
    for (var n = 0; n < 20; ++n)
        Check(PokerCards.ShuffleDeck().OrderBy(c => c).SequenceEqual(Enumerable.Range(0, 52)), "Deck permutation");
    Throws(() => PokerCards.Evaluate(new[] { 0, 0, 1, 2, 3 }));
    Throws(() => PokerCards.Evaluate(new[] { 0, 1, 2, 3, 52 }));
    Throws(() => PokerCards.Evaluate(new[] { 0, 1, 2, 3 }));
    Throws(() => new PokerTable(new PokerRules(MaxPlayers: 7)));
    Throws(() => new PokerTable(new PokerRules(SmallBlind: 0)));
    Check(PokerCards.Display(51) == "AS", "Card encoding");
}
static void HeadsUp()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    var view = View(table);
    Check(view.DealerSeat == 0 && view.ActingSeat == 0 && view.Pot == 15, "Heads-up order/blinds");
    Check(view.MyCards.Length == 2 && view.Seats.All(s => s.RevealedCards.Length == 0), "Private cards");
    Check(!view.MyCards.Intersect(View(table, 2).MyCards).Any(), "Distinct holes");
    Ok(Act(table, 1, PokerAction.Call));
    Check(View(table).Phase == PokerPhase.PreFlop && View(table).ActingSeat == 1, "Big blind option");
    Ok(Act(table, 2, PokerAction.Check));
    Check(View(table).Phase == PokerPhase.Flop && View(table).Board.Length == 3 && View(table).ActingSeat == 1, "Post-flop order");
    Finish(table);
    Check(Total(View(table)) == 2000 && View(table).Seats.All(s => s.RevealedCards.Length == 2), "Showdown");
}
static void FoldAndRefund()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 1, PokerAction.RaiseTo, 100));
    Ok(Act(table, 2, PokerAction.Fold));
    var view = View(table);
    Check(view.Phase == PokerPhase.Finished && view.Seats[0].Chips == 1010 && Total(view) == 2000, "Fold payout");
    Check(view.Payouts.Any(p => p.IsRefund && p.Chips == 90), "Uncalled refund");
    Check(view.Seats.All(s => s.RevealedCards.Length == 0), "No reveal on fold win");
}
static void Validation()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    var before = View(table);
    Check(Act(table, 2, PokerAction.Check) == PokerError.NotYourTurn, "Out of turn");
    Check(Act(table, 1, PokerAction.Check) == PokerError.IllegalAction, "Check facing bet");
    Check(Act(table, 1, PokerAction.RaiseTo, long.MaxValue) == PokerError.InvalidAmount, "Overbet");
    Check(Act(table, 1, PokerAction.RaiseTo, -1) == PokerError.InvalidAmount, "Negative bet");
    Check(Act(table, 1, PokerAction.RaiseTo, 11) == PokerError.InvalidAmount, "Small non-all-in raise");
    Check(View(table).Revision == before.Revision && Total(View(table)) == 2000, "Invalid actions changed state");
    Ok(Act(table, 1, PokerAction.Call));
    Check(table.Act(Id(1), before.HandId, before.Revision, PokerAction.Call, 0, Now()) == PokerError.StaleState, "Replay");
    Check(table.StartHand(Id(1), Now()) == PokerError.HandInProgress, "Reset active hand");
}
static void LateJoin()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    Ok(table.Join(Id(3), "Late player"));
    var late = View(table, 3);
    Check(late.MyCards.Length == 0 && !late.Seats.Single(s => s.PlayerId == Id(3)).InHand, "Late player was dealt in");
    var view = View(table);
    var original = view.MyCards[0];
    view.MyCards[0] = 99;
    Check(View(table).MyCards[0] == original, "Snapshot aliases private state");
    Finish(table);
    Check(View(table, 3).Seats.Single(s => s.PlayerId == Id(3)).Chips == 1000, "Late player paid in hand");
}
static void SidePots()
{
    // Three seats: dealing order 2,3,1. Player 1 AA wins main, player 2 KK wins side.
    var table = Table(3, "KC QC AC KD QD AD 2D 2C 3H 7S 4D 8C 5D 9H", new long[] { 50, 100, 200 });
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 1, PokerAction.RaiseTo, 50));
    Ok(Act(table, 2, PokerAction.RaiseTo, 100));
    Check(Act(table, 3, PokerAction.RaiseTo, 200) == PokerError.IllegalAction, "Dry side pot accepted");
    Ok(Act(table, 3, PokerAction.Call));
    var view = View(table);
    Check(view.Phase == PokerPhase.Finished && Total(view) == 350, "All-in runout");
    Check(view.Seats.Select(s => s.Chips).SequenceEqual(new long[] { 150, 100, 100 }), "Side pot distribution");
}
static void OddChip()
{
    // Everyone can play the royal-flush board. Player 1 contributes ten then folds.
    var table = Table(3, "2C 3C 4C 5C 6C 7C 8C TH JH QH 9C KH TC AH", new long[] { 100, 100, 100 });
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 1, PokerAction.Call));
    Ok(Act(table, 2, PokerAction.Call));
    Ok(Act(table, 3, PokerAction.Check));
    Ok(Act(table, 2, PokerAction.Check));
    Ok(Act(table, 3, PokerAction.Check));
    Ok(Act(table, 1, PokerAction.Fold));
    Finish(table);
    Check(Total(View(table)) == 300 && View(table).Seats[1].Chips == 105 && View(table).Seats[2].Chips == 105, "Board tie split");
    // A one-chip blind creates an actually odd main pot: player 2 folds after posting one.
    var odd = new PokerTable(new PokerRules(SmallBlind: 1, BigBlind: 2), () => Deck("2C 3C 4C 5C 6C 7C 8C TH JH QH 9C KH TC AH"));
    for (var i = 1; i <= 3; ++i) Ok(odd.Join(Id(i), "Player " + i, 100));
    Ok(odd.StartHand(Id(1), Now()));
    Ok(Act(odd, 1, PokerAction.Call));
    Ok(Act(odd, 2, PokerAction.Fold));
    Ok(Act(odd, 3, PokerAction.Check));
    Finish(odd);
    Check(View(odd).Seats.Select(s => s.Chips).SequenceEqual(new long[] { 100, 99, 101 }), "Odd chip must go left of dealer");
}
static void ShortRaise()
{
    var table = Table(3, stacks: new long[] { 100, 100, 15 });
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 1, PokerAction.Call));
    Ok(Act(table, 2, PokerAction.Call));
    Ok(Act(table, 3, PokerAction.RaiseTo, 15));
    Check(!View(table).CanRaise && View(table).ToCall == 5, "Short all-in reopened betting");
    Check(Act(table, 1, PokerAction.RaiseTo, 25) == PokerError.IllegalAction, "Illegal reopen accepted");
    Finish(table);
    Check(Total(View(table)) == 215, "Short raise chip conservation");
}
static void CumulativeRaise()
{
    // Four seats start with player 4. Player 4 calls 10; player 1 -> 15; player 2 -> 20.
    var table = Table(4, stacks: new long[] { 15, 20, 100, 100 });
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 4, PokerAction.Call));
    Ok(Act(table, 1, PokerAction.RaiseTo, 15));
    Ok(Act(table, 2, PokerAction.RaiseTo, 20));
    Ok(Act(table, 3, PokerAction.Call));
    Check(View(table, 4).CanRaise && View(table, 4).MinimumRaiseTo == 30, "Cumulative short all-ins did not reopen");
    Ok(Act(table, 4, PokerAction.RaiseTo, 30));
    Finish(table);
    Check(Total(View(table)) == 235, "Cumulative raise chip conservation");
}
static void Timeout()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    Check(!table.Tick(Now().AddSeconds(29)), "Premature timeout");
    Check(table.Tick(Now().AddSeconds(30)) && View(table).Phase == PokerPhase.Finished, "Timeout must fold facing a bet");
    Check(Total(View(table)) == 2000, "Timeout conservation");
}
static void ExpiredAction()
{
    var table = Table(2);
    Ok(table.StartHand(Id(1), Now()));
    Check(Act(table, 1, PokerAction.RaiseTo, 50, Now().AddSeconds(31)) == PokerError.StaleState, "Late packet accepted");
    Check(View(table).Phase == PokerPhase.Finished, "Deadline not enforced on receipt");
}
static void AllInLeave()
{
    var table = Table(3, "KC QC AC KD QD AD 2D 2C 3H 7S 4D 8C 5D 9H", new long[] { 50, 100, 200 });
    Ok(table.StartHand(Id(1), Now()));
    Ok(Act(table, 1, PokerAction.RaiseTo, 50));
    Ok(table.Leave(Id(1), Now()));
    Finish(table);
    Check(View(table).Seats[0].Chips == 150 && Total(View(table)) == 350, "Departing all-in lost eligibility");
}
static void Departure()
{
    var table = Table(3);
    Ok(table.StartHand(Id(1), Now()));
    Ok(table.Leave(Id(2), Now()));
    Check(View(table).ActingSeat == 0, "Other departure changed current turn");
    Ok(Act(table, 1, PokerAction.RaiseTo, 50));
    Check(View(table).ActingSeat == 2 && View(table).Seats[1].Folded, "Departing player did not auto-fold");
    Finish(table);
    Check(Total(View(table)) == 3000, "Departure conservation before removal");
}
static void ShortBlind()
{
    var table = Table(2, stacks: new long[] { 100, 3 });
    Ok(table.StartHand(Id(1), Now()));
    Check(View(table).Phase == PokerPhase.Finished && Total(View(table)) == 103, "Short BB blocked hand");
    Check(View(table).Payouts.Any(p => p.IsRefund && p.Chips == 2), "Uncalled small blind not returned");
}
static void RepeatedHands()
{
    var table = Table(3);
    for (var hand = 0; hand < 6; ++hand)
    {
        Ok(table.StartHand(Id(1), Now()));
        Check(View(table).DealerSeat == hand % 3 && View(table).HandId == hand + 1, "Dealer rotation");
        Finish(table);
        Check(Total(View(table)) == 3000, "Repeated-hand conservation");
    }
}
static void Simulations()
{
    var random = new Random(26719);
    for (var game = 0; game < 250; ++game)
    {
        var players = 2 + random.Next(5);
        var deck = Enumerable.Range(0, 52).ToArray();
        for (var i = 51; i > 0; --i) { var j = random.Next(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
        var table = new PokerTable(new PokerRules(), () => deck);
        long chips = 0;
        for (var i = 1; i <= players; ++i)
        {
            var stack = random.Next(1, 1001);
            chips += stack;
            Ok(table.Join(Id(i), "Player " + i, stack));
        }
        Ok(table.StartHand(Id(1), Now()));
        var moves = 0;
        while (View(table).Phase != PokerPhase.Finished && ++moves <= 500)
        {
            var view = View(table);
            Check(Total(view) == chips && view.Seats.All(s => s.Chips >= 0), "Chip invariant mid-hand");
            Check(view.Seats.All(s => s.RevealedCards.Length == 0), "Opponent cards revealed mid-hand");
            var actor = view.Seats.Single(s => s.Seat == view.ActingSeat).PlayerId;
            var own = table.Snapshot(actor);
            var action = own.ToCall > 0 ? PokerAction.Call : PokerAction.Check;
            long amount = 0;
            var choice = random.Next(5);
            if (choice == 0) action = PokerAction.Fold;
            else if (choice == 1 && own.CanRaise)
            {
                action = PokerAction.RaiseTo;
                amount = random.Next(2) == 0 ? own.MaximumRaiseTo : Math.Min(own.MinimumRaiseTo, own.MaximumRaiseTo);
            }
            Ok(table.Act(actor, own.HandId, own.Revision, action, amount, Now()));
        }
        Check(moves <= 500 && Total(View(table)) == chips, "Simulation did not finish/conserve chips");
    }
}
