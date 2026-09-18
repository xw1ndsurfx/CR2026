using System.Runtime.CompilerServices;
using Intersect.Server.MiniGames.Poker;

internal static class ExtrasSmokeTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("Net win excludes the blind and an unmatched refund; only winner gets effect", () =>
            {
                var f = new Fixture(); f.Start(); f.FoldActor();
                var win = f.Tables.CollectWins().Single();
                Check(win.Recipient == f.B.Session && win.NetChips == 5 && win.PlayerName == "Bob" && win.AnnounceGlobally, "Wrong net winner");
                var a = f.Tables.Presentation(f.A, f.Id); var b = f.Tables.Presentation(f.B, f.Id);
                Check(a.NetWin == 0 && a.VictoryAnimationId == Guid.Empty, "Loser got celebration");
                Check(b.NetWin == 5 && b.VictoryAnimationId == f.Animation, "Winner metadata missing");
            }),
            ("Refresh, stale action and repeated sweep never duplicate a notice", () =>
            {
                var f = new Fixture(); f.Start(); f.FoldActor();
                Check(f.Tables.CollectWins().Length == 1, "First notice missing");
                for (var i = 0; i < 10; ++i)
                {
                    f.Tables.Snapshot(f.A, f.Id);
                    f.Tables.Act(f.A, f.Id, 1, 0, PokerAction.Fold, 0, f.Now);
                    f.Tables.Tick(f.Now, _ => true);
                    Check(f.Tables.CollectWins().Length == 0, "Duplicate notice");
                }
                f.Start(); f.FoldActor();
                Check(f.Tables.CollectWins().Single().HandId == 2, "Next hand suppressed");
            }),
            ("A silent table still exposes the winner privately but does not request global chat", () =>
            {
                var f = new Fixture(announce: false); f.Start(); f.FoldActor();
                Check(!f.Tables.CollectWins().Single().AnnounceGlobally, "Opt-in ignored");
            }),
            ("Card backs are pinned during a hand and selected changes apply next hand", () =>
            {
                var f = new Fixture();
                Check(f.Tables.SelectCardBack(f.A, f.Id, 1).Error == PokerRegistryError.None, "Select failed");
                f.Start(); var before = f.State(f.A);
                f.Tables.SelectCardBack(f.A, f.Id, 2);
                var back = f.Tables.Presentation(f.B, f.Id).CardBacks.Single(s => s.PlayerId == f.A.Session.PlayerId);
                Check(back.CurrentId == 1 && back.SelectedId == 2, "Live back changed");
                Check(f.State(f.A).Revision == before.Revision && f.State(f.A).Pot == before.Pot, "Cosmetic altered betting state");
                f.FoldActor(); f.Start();
                Check(f.Tables.Presentation(f.B, f.Id).CardBacks.Single(s => s.PlayerId == f.A.Session.PlayerId).CurrentId == 2, "New back not applied");
            }),
            ("Forged catalog, session and table selections are rejected", () =>
            {
                var f = new Fixture();
                Check(f.Tables.SelectCardBack(f.A, f.Id, 4).Detail == PokerError.InvalidAmount, "Unknown back accepted");
                Check(f.Tables.SelectCardBack(f.A, f.Id, -1).Detail == PokerError.InvalidAmount, "Negative back accepted");
                var fake = f.A with { Session = new(f.A.Session.PlayerId, Guid.NewGuid()) };
                Check(f.Tables.SelectCardBack(fake, f.Id, 1).Error == PokerRegistryError.SessionChanged, "Forged session accepted");
                Check(f.Tables.SelectCardBack(f.A, Guid.NewGuid(), 1).Error == PokerRegistryError.WrongTable, "Wrong table accepted");
                Check(f.State(f.A).Seats.Sum(s => s.Chips) == 2000, "Selection minted chips");
            }),
            ("Departure settlement is captured before membership cleanup and not replayed on rejoin", () =>
            {
                var f = new Fixture(); f.Start();
                f.Tables.Leave(f.A.Session, f.Now);
                Check(f.Tables.CollectWins().Single().NetChips == 5, "Departure lost winner");
                f.Tables.Leave(f.B.Session, f.Now);
                Check(f.Tables.TableCount == 0 && f.Tables.CollectWins().Length == 0, "Cleanup replayed win");
            }),
            ("Leaving an all-in seat preserves a possible win and its net amount", () =>
            {
                var f = new Fixture(); f.Start(); var state = f.State(f.A);
                Check(f.Tables.Act(f.A, f.Id, state.HandId, state.Revision, PokerAction.RaiseTo, 1000, f.Now).Detail == PokerError.None, "All-in rejected");
                f.Tables.Leave(f.A.Session, f.Now); state = f.State(f.B);
                f.Tables.Act(f.B, f.Id, state.HandId, state.Revision, PokerAction.Call, 0, f.Now);
                var b = f.State(f.B).Seats.Single(s => s.PlayerId == f.B.Session.PlayerId).Chips;
                var wins = f.Tables.CollectWins();
                if (b == 1000) Check(wins.Length == 0, "A split returning stakes was called profit");
                else Check(wins.Length == 1 && wins[0].NetChips == 1000 &&
                    wins[0].Recipient.PlayerId == (b == 2000 ? f.B.Session.PlayerId : f.A.Session.PlayerId), "All-in net winner wrong");
            }),
            ("NPC backs are configured but NPC wins never become human announcements", () =>
            {
                var tables = new PokerTableRegistry(); var a = Presence(Guid.NewGuid());
                var join = tables.Join(a, "npc", "Alice", new PokerRules(), new PokerTableOptions(DealerPlays: true, AnnounceWins: true, NpcCardBackId: 3));
                var id = join.TableInstanceId;
                Check(tables.Presentation(a, id).CardBacks.Single(b => b.PlayerId != a.Session.PlayerId).CurrentId == 3, "NPC back missing");
                tables.StartHand(a, id, join.Snapshot!.Revision, DateTimeOffset.UtcNow);
                var state = tables.Snapshot(a, id).Snapshot!;
                tables.Act(a, id, state.HandId, state.Revision, PokerAction.Fold, 0, DateTimeOffset.UtcNow);
                Check(tables.CollectWins().Length == 0, "NPC winner broadcast");
            }),
            ("Pure ledger handles multiple positive split winners and refund-only returns", () =>
            {
                var f = new Fixture(); var before = f.State(f.A);
                var ledger = new PokerHandLedger(); ledger.Begin(before, 1);
                var seats = before.Seats.Select(s => s with { Chips = s.Chips + 10 }).ToArray();
                var after = before with { HandId = 1, Phase = PokerPhase.Finished, Seats = seats,
                    Payouts = seats.Select(s => new PokerPayout(s.PlayerId, 60, false)).ToArray() };
                Check(ledger.Complete(after).All(w => w.Chips == 10) && ledger.NetWin(f.A.Session.PlayerId, 1) == 10, "Split net accounting");
                Check(ledger.Complete(after).Length == 0, "Ledger replay");
                ledger.Begin(before with { HandId = 1 }, 2);
                var refunds = after with { HandId = 2, Payouts = seats.Select(s => new PokerPayout(s.PlayerId, 60, true)).ToArray() };
                Check(ledger.Complete(refunds).Length == 0, "Refund-only hand celebrated");
            }),
        };
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("PASS EXTRAS: " + name); }
            catch (Exception ex) { ++failures; Console.Error.WriteLine("FAIL EXTRAS: " + name + "\n" + ex); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} poker extras groups passed.");
        if (failures != 0) throw new InvalidOperationException("Poker extras regression failed.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static PokerPresence Presence(Guid map) => new(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
    private sealed class Fixture
    {
        public readonly PokerTableRegistry Tables = new();
        public readonly PokerPresence A, B;
        public readonly Guid Id, Animation = Guid.NewGuid();
        public readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        public Fixture(bool announce = true)
        {
            var map = Guid.NewGuid(); A = Presence(map); B = Presence(map);
            var options = new PokerTableOptions(AnnounceWins: announce, VictoryAnimationId: Animation);
            var rules = new PokerRules(MaxPlayers: 2);
            Id = Tables.Join(A, "extras", "Alice", rules, options).TableInstanceId;
            Check(Tables.Join(B, "extras", "Bob", rules, options).Error == PokerRegistryError.None, "Join failed");
        }
        public PokerSnapshot State(PokerPresence p) => Tables.Snapshot(p, Id).Snapshot!;
        public void Start()
        {
            Check(Tables.StartHand(A, Id, State(A).Revision, Now).Error == PokerRegistryError.None, "Start failed");
        }
        public void FoldActor()
        {
            var state = State(A); var id = state.Seats.Single(s => s.Seat == state.ActingSeat).PlayerId;
            var p = id == A.Session.PlayerId ? A : B;
            Check(Tables.Act(p, Id, state.HandId, state.Revision, PokerAction.Fold, 0, Now).Error == PokerRegistryError.None, "Fold failed");
        }
    }
}
