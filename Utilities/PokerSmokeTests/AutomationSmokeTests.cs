using System.Runtime.CompilerServices;
using Intersect.Server.MiniGames.Poker;

internal static class AutomationSmokeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private static PokerPresence Person(Guid map) => new(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static PokerSnapshot State(PokerTableRegistry registry, PokerPresence person, Guid id) =>
        registry.Snapshot(person, id).Snapshot ?? throw new InvalidOperationException("Missing snapshot");

    [ModuleInitializer]
    internal static void Run()
    {
        var count = 0;
        void Test(string name, Action action)
        {
            try { action(); ++count; Console.WriteLine("PASS AUTO: " + name); }
            catch (Exception ex) { Console.Error.WriteLine("FAIL AUTO: " + name + "\n" + ex); throw; }
        }
        Test("Legacy tables remain human-only and manual", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            var join = r.Join(p, "old", "Player", new());
            r.Tick(Now.AddHours(1), _ => true);
            Check(State(r, p, join.TableInstanceId).Seats.Length == 1 && State(r, p, join.TableInstanceId).HandId == 0, "Legacy behavior changed");
        });
        Test("Dealer and guests share table but are not authenticated members", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            var o = new PokerTableOptions(true, 2, true, Guid.NewGuid());
            var a = r.Join(p, "npc", "Player", new(), o);
            var meta = r.Presentation(p, a.TableInstanceId);
            Check(a.Error == PokerRegistryError.None && a.Snapshot!.Seats.Length == 4, "Wrong seats");
            Check(r.MemberCount == 1 && r.Memberships().Length == 1 && meta.NpcIds.Length == 3, "Bots became clients");
            Check(meta.NpcIds.Contains(meta.DealerNpcId) && meta.DealAnimationId == o.DealAnimationId, "Missing dealer/animation");
            meta.NpcIds[0] = Guid.Empty;
            Check(r.Presentation(p, a.TableInstanceId).NpcIds.All(id => id != Guid.Empty), "Presentation aliases registry");
            var b = r.Join(p, "npc", "Player", new(), o);
            Check(b.Snapshot!.Revision == a.Snapshot!.Revision && b.Snapshot.Seats.Length == 4, "Rejoin granted seats/chips");
        });
        Test("All optional settings participate in shared-table conflict checks", () =>
        {
            var map = Guid.NewGuid(); var r = new PokerTableRegistry(); var p = Person(map); var q = Person(map);
            var o = new PokerTableOptions(true, 1, true, Guid.NewGuid());
            r.Join(p, "same", "P", new(), o);
            foreach (var bad in new[] { o with { DealerPlays = false }, o with { NpcPlayers = 0 },
                         o with { AutoStart = false }, o with { DealAnimationId = Guid.Empty } })
                Check(r.Join(q, "same", "Q", new(), bad).Error == PokerRegistryError.RulesConflict, "Conflict not enforced");
            Check(r.MemberCount == 1, "Conflicting join mutated membership");
        });
        Test("Solo player automatically receives a private hand after the pause", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            var a = r.Join(p, "solo", "P", new(), new(true, 0, true));
            r.Tick(Now, _ => true); r.Tick(Now.AddSeconds(4), _ => true);
            Check(State(r, p, a.TableInstanceId).HandId == 0, "Started before pause");
            r.Tick(Now.AddSeconds(5), _ => true);
            var s = State(r, p, a.TableInstanceId);
            Check(s.HandId == 1 && s.MyCards.Length == 2 && s.Seats.Length == 2, "No solo hand");
            Check(s.Seats.All(seat => seat.RevealedCards.Length == 0), "NPC/private cards leaked");
            Check(r.CollectUpdates().All(d => d.Recipient == p.Session), "Delivery addressed to NPC");
        });
        Test("Dealer takes a paced legal action rather than requiring a client", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            var a = r.Join(p, "paced", "P", new(), new(true));
            r.StartHand(p, a.TableInstanceId, a.Snapshot!.Revision, Now);
            var s = State(r, p, a.TableInstanceId);
            if (s.Seats.Single(x => x.PlayerId == p.Session.PlayerId).Seat == s.ActingSeat)
                r.Act(p, a.TableInstanceId, s.HandId, s.Revision, s.ToCall > 0 ? PokerAction.Call : PokerAction.Check, 0, Now);
            s = State(r, p, a.TableInstanceId);
            Check(s.ActingSeat == s.Seats.Single(x => x.PlayerId != p.Session.PlayerId).Seat, "Dealer has no turn");
            var revision = s.Revision;
            r.Tick(Now.AddSeconds(1), _ => true);
            Check(State(r, p, a.TableInstanceId).Revision == revision, "NPC acted without delay");
            r.Tick(Now.AddSeconds(3), _ => true);
            Check(State(r, p, a.TableInstanceId).Revision > revision, "NPC did not act");
        });
        Test("Human replaces a guest between hands, never the dealer", () =>
        {
            var r = new PokerTableRegistry(); var map = Guid.NewGuid(); var p = Person(map); var q = Person(map);
            var rules = new PokerRules(MaxPlayers: 3); var o = new PokerTableOptions(true, 1);
            var a = r.Join(p, "full", "P", rules, o); var dealer = r.Presentation(p, a.TableInstanceId).DealerNpcId;
            Check(r.Join(q, "full", "Q", rules, o).Error == PokerRegistryError.None, "Guest did not yield");
            Check(r.Presentation(p, a.TableInstanceId).NpcIds.SequenceEqual(new[] { dealer }), "Dealer was evicted");
            Check(r.Join(Person(map), "full", "R", rules, o).Detail == PokerError.TableFull, "Dealer reservation lost");
        });
        Test("Full active table never evicts an NPC during a hand", () =>
        {
            var r = new PokerTableRegistry(); var map = Guid.NewGuid(); var p = Person(map);
            var rules = new PokerRules(MaxPlayers: 3); var o = new PokerTableOptions(true, 1);
            var a = r.Join(p, "active", "P", rules, o);
            r.StartHand(p, a.TableInstanceId, a.Snapshot!.Revision, Now);
            var ids = r.Presentation(p, a.TableInstanceId).NpcIds;
            Check(r.Join(Person(map), "active", "Q", rules, o).Detail == PokerError.TableFull, "Evicted active seat");
            Check(ids.SequenceEqual(r.Presentation(p, a.TableInstanceId).NpcIds) && r.MemberCount == 1, "Active table mutated");
        });
        Test("Configured guests return after a human vacates their idle seat", () =>
        {
            var r = new PokerTableRegistry(); var map = Guid.NewGuid(); var p = Person(map); var q = Person(map);
            var rules = new PokerRules(MaxPlayers: 2); var o = new PokerTableOptions(false, 1, true);
            var a = r.Join(p, "return", "P", rules, o);
            r.Join(q, "return", "Q", rules, o); r.Leave(q.Session, Now);
            r.Tick(Now, _ => true); r.Tick(Now.AddSeconds(5), _ => true);
            var s = State(r, p, a.TableInstanceId);
            Check(s.HandId == 1 && s.Seats.Length == 2, "Guest replacement failed to resume solo play");
        });
        Test("Disconnect settles the existing hand then removes the NPC table", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            r.Join(p, "disconnect", "P", new(), new(true, 2, true));
            r.Tick(Now, _ => true); r.Tick(Now.AddSeconds(5), _ => true);
            for (var i = 0; i < 500 && r.TableCount > 0; ++i) r.Tick(Now.AddSeconds(7 + i * 2), _ => false);
            Check(r.TableCount == 0 && r.MemberCount == 0 && r.CollectUpdates().Length == 0, "Bot-only table kept running");
        });
        Test("Invalid settings, invalid names and forged NPC sessions cannot mutate tables", () =>
        {
            var r = new PokerTableRegistry(); var map = Guid.NewGuid(); var p = Person(map);
            var rules = new PokerRules(MaxPlayers: 3); var o = new PokerTableOptions(true, 1);
            Check(r.Join(p, "bad", "P", rules, new(true, 2)).Error == PokerRegistryError.InvalidRules, "No human seat reserved");
            var a = r.Join(p, "safe", "P", rules, o); var meta = r.Presentation(p, a.TableInstanceId);
            Check(r.Join(Person(map), "safe", "", rules, o).Detail == PokerError.InvalidPlayer, "Accepted invalid name");
            Check(meta.NpcIds.SequenceEqual(r.Presentation(p, a.TableInstanceId).NpcIds), "Invalid join evicted guest");
            var forged = new PokerPresence(new(meta.DealerNpcId, Guid.NewGuid()), map, Guid.Empty);
            Check(r.Snapshot(forged, a.TableInstanceId).Error == PokerRegistryError.NotSeated, "NPC authenticated as human");
        });
        Test("NPC policy ignores opponents' cards and has legal deterministic fallbacks", () =>
        {
            var human = Guid.NewGuid(); var npc = Guid.NewGuid(); var table = new PokerTable(new());
            table.Join(human, "Human"); table.Join(npc, "Dealer"); table.StartHand(human, Now);
            var s = table.Snapshot(human);
            table.Act(human, s.HandId, s.Revision, PokerAction.Call, 0, Now);
            var view = table.Snapshot(npc);
            var other = view with { Seats = view.Seats.Select(x => x.PlayerId == human ? x with { RevealedCards = new[] { 12, 25 } } : x).ToArray() };
            for (var roll = 0; roll < 100; ++roll)
                Check(PokerNpcPolicy.Choose(view, npc, 10, roll) == PokerNpcPolicy.Choose(other, npc, 10, roll), "Policy reads opponent cards");
            var decision = PokerNpcPolicy.Choose(view, npc, 10, 90);
            Check(table.Act(npc, view.HandId, view.Revision, decision.Action, decision.Amount, Now) == PokerError.None, "Policy made illegal move");
        });
        Test("NPC all-in response is cautious without changing card odds", () =>
        {
            var human = Guid.NewGuid(); var npc = Guid.NewGuid();
            PokerSnapshot View(int[] cards) => new(
                1, 1, PokerPhase.PreFlop, 0, 1, 900, 900, 890, 1000, 1000, false,
                Now.AddSeconds(30), [], cards,
                [
                    new PokerSeatView(0, human, "Human", 0, 900, 1000, true, false, true, false, []),
                    new PokerSeatView(1, npc, "NPC", 990, 10, 10, true, false, false, false, [])
                ], []);
            var weakFold = PokerNpcPolicy.Choose(View([0, 18]), npc, 10, 50);
            var weakBluffCatch = PokerNpcPolicy.Choose(View([0, 18]), npc, 10, 5);
            var strong = PokerNpcPolicy.Choose(View([12, 25]), npc, 10, 50);
            Check(weakFold.Action == PokerAction.Fold, "Weak NPC called too freely against an expensive all-in");
            Check(weakBluffCatch.Action == PokerAction.Call, "Weak NPC never bluff-catches repetitive all-ins");
            Check(strong.Action == PokerAction.Call, "Strong NPC refused an all-in it should defend");
            var deck = PokerCards.ShuffleDeck();
            Check(deck.Length == 52 && deck.Distinct().Count() == 52 && deck.All(c => c is >= 0 and < 52),
                "All-in policy altered the fair deck contract");
        });
        Test("Five automatic hands preserve chips and private-card boundaries", () =>
        {
            var r = new PokerTableRegistry(); var p = Person(Guid.NewGuid());
            var a = r.Join(p, "continuous", "P", new(), new(true, 0, true));
            var finished = new HashSet<long>();
            for (var i = 0; i < 1000 && finished.Count < 5; ++i)
            {
                var now = Now.AddSeconds(i * 2); r.Tick(now, _ => true);
                var s = State(r, p, a.TableInstanceId);
                Check(s.Seats.Sum(x => x.Chips) + s.Pot == 2000, "Chips changed during five low-stakes hands");
                if (s.Phase == PokerPhase.Finished) { finished.Add(s.HandId); continue; }
                if (s.Phase == PokerPhase.Waiting) continue;
                Check(s.Seats.All(x => x.RevealedCards.Length == 0), "Private cards leaked before showdown");
                var me = s.Seats.Single(x => x.PlayerId == p.Session.PlayerId);
                if (s.ActingSeat == me.Seat)
                    Check(r.Act(p, a.TableInstanceId, s.HandId, s.Revision,
                        s.ToCall > 0 ? PokerAction.Call : PokerAction.Check, 0, now).Error == PokerRegistryError.None, "Human action rejected");
            }
            Check(finished.Count == 5, "Continuous table stalled");
        });
        Console.WriteLine($"{count}/{count} poker automation test groups passed.");
    }
}
