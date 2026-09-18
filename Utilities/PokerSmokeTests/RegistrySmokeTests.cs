using System.Runtime.CompilerServices;
using Intersect.Server.MiniGames.Poker;

internal static class RegistrySmokeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Map = Guid.NewGuid();
    private static int _passed;
    private static PokerPresence Person() => new(new(Guid.NewGuid(), Guid.NewGuid()), Map, Guid.Empty);
    private static void Assert(bool value, string message = "Assertion failed")
    { if (!value) throw new InvalidOperationException(message); }
    private static PokerRegistryResult Ok(PokerRegistryResult result)
    { Assert(result.Error == PokerRegistryError.None, $"{result.Error}: {result.Detail}"); return result; }
    private static PokerSnapshot State(PokerTableRegistry r, PokerPresence p, Guid id) => Ok(r.Snapshot(p, id)).Snapshot!;
    private static void Run(string name, Action test)
    {
        try { test(); ++_passed; Console.WriteLine("PASS registry: " + name); }
        catch (Exception ex) { throw new InvalidOperationException("FAILED registry: " + name, ex); }
    }

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("shared identity and idempotent event activation", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            var x = Ok(r.Join(a, "tavern-1", "Alice", rules));
            var y = Ok(r.Join(b, "tavern-1", "Bob", rules));
            var again = Ok(r.Join(a, "tavern-1", "Alice", rules));
            Assert(x.TableInstanceId == y.TableInstanceId && again.TableInstanceId == x.TableInstanceId);
            var snapshot = again.Snapshot ?? throw new InvalidOperationException("Successful join has no snapshot");
            Assert(r.TableCount == 1 && r.MemberCount == 2 && snapshot.Seats.Length == 2);
            Assert(snapshot.Seats.Single(s => s.PlayerId == a.Session.PlayerId).Chips == 1000);
        });
        Run("map, instance and table-name isolation", () =>
        {
            var r = new PokerTableRegistry(); var rules = new PokerRules();
            var players = new[] { Person(), Person() with { MapId = Guid.NewGuid() },
                Person() with { MapInstanceId = Guid.NewGuid() }, Person() };
            var ids = players.Select((p, i) => Ok(r.Join(p, i == 3 ? "other" : "table", "Player", rules)).TableInstanceId).ToArray();
            Assert(ids.Distinct().Count() == 4 && r.TableCount == 4);
        });
        Run("conflicting event settings never replace a table", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            var x = Ok(r.Join(a, "table", "Alice", rules));
            Assert(r.Join(b, "table", "Bob", rules with { SmallBlind = 6 }).Error == PokerRegistryError.RulesConflict);
            Assert(r.Join(a, "table", "Alice", rules with { StartingChips = 2000 }).Error == PokerRegistryError.RulesConflict);
            Assert(r.MemberCount == 1 && State(r, a, x.TableInstanceId).Seats[0].Chips == 1000);
        });
        Run("validation and bounded registry capacity", () =>
        {
            var r = new PokerTableRegistry(1); var a = Person(); var b = Person(); var rules = new PokerRules();
            Assert(r.Join(a, "bad table", "Alice", rules).Error == PokerRegistryError.InvalidTableName);
            Assert(r.Join(a, new string('x', 65), "Alice", rules).Error == PokerRegistryError.InvalidTableName);
            Assert(r.Join(a with { MapId = Guid.Empty }, "table", "Alice", rules).Error == PokerRegistryError.InvalidPresence);
            Assert(r.Join(a, "table", "Alice", rules with { BigBlind = 0 }).Error == PokerRegistryError.InvalidRules);
            Assert(r.Join(a, "table", "", rules).Detail == PokerError.InvalidPlayer);
            Assert(r.TableCount == 0 && r.MemberCount == 0);
            Ok(r.Join(a, "table", "Alice", rules));
            Assert(r.Join(b, "other", "Bob", rules).Error == PokerRegistryError.Capacity);
            Ok(r.Join(b, "table", "Bob", rules));
            Assert(r.TableCount == 1 && r.MemberCount == 2);
        });
        Run("full table creates no ghost membership", () =>
        {
            var r = new PokerTableRegistry(); var rules = new PokerRules(MaxPlayers: 2);
            Ok(r.Join(Person(), "table", "Alice", rules)); Ok(r.Join(Person(), "table", "Bob", rules));
            var c = Person(); var rejected = r.Join(c, "table", "Carol", rules);
            Assert(rejected.Error == PokerRegistryError.PokerRejected && rejected.Detail == PokerError.TableFull);
            Assert(r.MemberCount == 2); Ok(r.Join(c, "other", "Carol", rules));
        });
        Run("session, location and table-instance authorization", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var rules = new PokerRules();
            var x = Ok(r.Join(a, "table", "Alice", rules));
            var impostor = a with { Session = new(a.Session.PlayerId, Guid.NewGuid()) };
            Assert(r.Join(a, "other", "Alice", rules).Error == PokerRegistryError.AlreadyAtAnotherTable);
            Assert(r.Snapshot(impostor, x.TableInstanceId).Error == PokerRegistryError.SessionChanged);
            Assert(r.Leave(impostor.Session, Now).Error == PokerRegistryError.SessionChanged);
            Assert(r.Snapshot(a with { MapId = Guid.NewGuid() }, x.TableInstanceId).Error == PokerRegistryError.WrongLocation);
            Assert(r.Snapshot(a, Guid.NewGuid()).Error == PokerRegistryError.WrongTable);
            Assert(r.Snapshot(Person(), x.TableInstanceId).Error == PokerRegistryError.NotSeated);
            Assert(r.MemberCount == 1);
        });
        Run("start and actions reject stale revisions and wrong turns", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            var x = Ok(r.Join(a, "table", "Alice", rules)); Ok(r.Join(b, "table", "Bob", rules));
            Assert(r.StartHand(a, x.TableInstanceId, x.Snapshot!.Revision, Now).Detail == PokerError.StaleState);
            var s = Ok(r.StartHand(a, x.TableInstanceId, State(r, a, x.TableInstanceId).Revision, Now)).Snapshot!;
            var actor = s.Seats.Single(v => v.Seat == s.ActingSeat).PlayerId == a.Session.PlayerId ? a : b;
            var other = actor == a ? b : a;
            Assert(r.Act(other, x.TableInstanceId, s.HandId, s.Revision, PokerAction.Fold, 0, Now).Detail == PokerError.NotYourTurn);
            s = State(r, actor, x.TableInstanceId);
            Ok(r.Act(actor, x.TableInstanceId, s.HandId, s.Revision,
                s.ToCall == 0 ? PokerAction.Check : PokerAction.Call, 0, Now));
            Assert(r.Act(actor, x.TableInstanceId, s.HandId, s.Revision, PokerAction.Fold, 0, Now).Detail == PokerError.StaleState);
        });
        Run("recipient-specific detached snapshots and revision updates", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            var x = Ok(r.Join(a, "table", "Alice", rules)); Ok(r.Join(b, "table", "Bob", rules));
            Ok(r.StartHand(a, x.TableInstanceId, State(r, a, x.TableInstanceId).Revision, Now));
            var deliveries = r.CollectUpdates(); Assert(deliveries.Length == 2);
            foreach (var d in deliveries)
            {
                Assert(d.Snapshot.MyCards.Length == 2 && d.Snapshot.Seats.All(s => s.RevealedCards.Length == 0));
                var p = d.Recipient == a.Session ? a : b;
                var card = d.Snapshot.MyCards[0]; d.Snapshot.MyCards[0] = -1;
                Assert(State(r, p, x.TableInstanceId).MyCards[0] == card);
            }
            Assert(r.CollectUpdates().Length == 0);
            var c = Person(); var late = Ok(r.Join(c, "table", "Carol", rules));
            Assert(late.Snapshot!.MyCards.Length == 0 && !late.Snapshot.Seats.Single(s => s.PlayerId == c.Session.PlayerId).InHand);
            Assert(r.CollectUpdates().Length == 3);
        });
        Run("departing active-hand seats remain reserved until settlement", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var c = Person(); var rules = new PokerRules();
            var people = new[] { a, b, c }; var id = Ok(r.Join(a, "table", "Alice", rules)).TableInstanceId;
            Ok(r.Join(b, "table", "Bob", rules)); Ok(r.Join(c, "table", "Carol", rules));
            Ok(r.StartHand(a, id, State(r, a, id).Revision, Now));
            Ok(r.Leave(c.Session, Now));
            Assert(r.MemberCount == 3 && r.Join(c, "other", "Carol", rules).Error == PokerRegistryError.Leaving);
            Assert(State(r, a, id).Seats.Single(s => s.PlayerId == c.Session.PlayerId).Leaving);
            for (var i = 0; i < 12 && State(r, a, id).Phase != PokerPhase.Finished; ++i)
            {
                var s = State(r, a, id); var actorId = s.Seats.Single(v => v.Seat == s.ActingSeat).PlayerId;
                var actor = people.Single(p => p.Session.PlayerId == actorId);
                Ok(r.Act(actor, id, s.HandId, s.Revision, PokerAction.Fold, 0, Now));
            }
            Assert(State(r, a, id).Phase == PokerPhase.Finished && r.MemberCount == 2);
            Assert(r.Snapshot(c, id).Error == PokerRegistryError.NotSeated);
        });
        Run("offline or moved players release waiting seats and empty tables", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            Ok(r.Join(a, "table", "Alice", rules)); Ok(r.Join(b, "table", "Bob", rules));
            r.Tick(Now, p => p == a); Assert(r.MemberCount == 1);
            r.Tick(Now, _ => false); Assert(r.MemberCount == 0 && r.TableCount == 0);
            Assert(r.CollectUpdates().Length == 0);
        });
        Run("presence callback runs outside lock and cannot evict a racing new membership", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var rules = new PokerRules();
            Ok(r.Join(a, "old", "Alice", rules)); Guid replacement = Guid.Empty;
            r.Tick(Now, p =>
            {
                var work = Task.Run(() =>
                {
                    Ok(r.Leave(p.Session, Now));
                    return Ok(r.Join(p, "new", "Alice", rules)).TableInstanceId;
                });
                Assert(work.Wait(TimeSpan.FromSeconds(3)), "Presence callback held registry lock");
                replacement = work.Result;
                return false; // Stale observation about the old membership.
            });
            Assert(r.MemberCount == 1 && r.TableCount == 1); Ok(r.Snapshot(a, replacement));
        });
        Run("concurrent distinct joins share one bounded table", () =>
        {
            var r = new PokerTableRegistry(); var results = new PokerRegistryResult[64];
            Parallel.For(0, results.Length, i => results[i] = r.Join(Person(), "shared", "Player", new PokerRules()));
            var accepted = results.Where(x => x.Error == PokerRegistryError.None).ToArray();
            Assert(accepted.Length == 6 && accepted.Select(x => x.TableInstanceId).Distinct().Count() == 1);
            Assert(r.TableCount == 1 && r.MemberCount == 6);
            Assert(results.Where(x => x.Error != PokerRegistryError.None).All(x => x.Detail == PokerError.TableFull));
        });
        Run("replacement table rejects old instance IDs", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var rules = new PokerRules();
            var old = Ok(r.Join(a, "table", "Alice", rules)).TableInstanceId;
            Ok(r.Leave(a.Session, Now)); Assert(r.TableCount == 0);
            var fresh = Ok(r.Join(a, "table", "Alice", rules)).TableInstanceId;
            Assert(fresh != old && r.Snapshot(a, old).Error == PokerRegistryError.WrongTable);
        });
        Run("server ticks advance silent hands and all-disconnect cleanup", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            var id = Ok(r.Join(a, "table", "Alice", rules)).TableInstanceId; Ok(r.Join(b, "table", "Bob", rules));
            Ok(r.StartHand(a, id, State(r, a, id).Revision, Now));
            r.Tick(Now.AddHours(1), _ => true);
            Assert(State(r, a, id).Phase == PokerPhase.Finished);
            r.Tick(Now.AddHours(2), _ => false); Assert(r.TableCount == 0 && r.MemberCount == 0);
        });
        Run("concurrent repeated activation never duplicates starting chips", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var results = new PokerRegistryResult[32];
            Parallel.For(0, results.Length, i => results[i] = r.Join(a, "table", "Alice", new PokerRules()));
            Assert(results.All(x => x.Error == PokerRegistryError.None));
            Assert(r.MemberCount == 1 && r.TableCount == 1);
            Assert(results.All(x => x.Snapshot!.Seats.Length == 1 && x.Snapshot.Seats[0].Chips == 1000));
        });
        Run("late joiners can leave without becoming ghost active seats", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var c = Person(); var rules = new PokerRules();
            var id = Ok(r.Join(a, "table", "Alice", rules)).TableInstanceId; Ok(r.Join(b, "table", "Bob", rules));
            Ok(r.StartHand(a, id, State(r, a, id).Revision, Now)); Ok(r.Join(c, "table", "Carol", rules));
            Ok(r.Leave(c.Session, Now));
            Assert(r.MemberCount == 2 && State(r, a, id).Phase == PokerPhase.PreFlop);
        });
        Run("failed presence capture causes no partial eviction", () =>
        {
            var r = new PokerTableRegistry(); var a = Person(); var b = Person(); var rules = new PokerRules();
            Ok(r.Join(a, "table", "Alice", rules)); Ok(r.Join(b, "table", "Bob", rules));
            var calls = 0;
            try { r.Tick(Now, _ => ++calls == 1 ? false : throw new InvalidOperationException("capture")); }
            catch (InvalidOperationException ex) when (ex.Message == "capture") { }
            Assert(calls == 2 && r.MemberCount == 2);
        });
        Run("old sessions cannot leave or read a new login's seat", () =>
        {
            var r = new PokerTableRegistry(); var old = Person(); var rules = new PokerRules();
            Ok(r.Join(old, "table", "Alice", rules)); r.Tick(Now, _ => false);
            var fresh = old with { Session = new(old.Session.PlayerId, Guid.NewGuid()) };
            var id = Ok(r.Join(fresh, "table", "Alice", rules)).TableInstanceId;
            Assert(r.Leave(old.Session, Now).Error == PokerRegistryError.SessionChanged);
            Assert(r.Snapshot(old, id).Error == PokerRegistryError.SessionChanged);
            Ok(r.Snapshot(fresh, id));
        });
        Console.WriteLine($"Registry: {_passed} test groups passed.");
    }
}
