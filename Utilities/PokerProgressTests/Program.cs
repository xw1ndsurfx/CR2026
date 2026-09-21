using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();
var root = Path.Combine(Path.GetTempPath(), "poker-progression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0; var failed = 0;
try
{
    Run("All level thresholds and six unlock boundaries", () =>
    {
        for (var level = 1; level <= 25; ++level)
        {
            var xp = MiniGameProgression.ExperienceAtLevel(level);
            Check(MiniGameProgression.Level(xp) == level, "Wrong level boundary");
            if (level > 1) Check(MiniGameProgression.Level(xp - 1) == level - 1, "Early level");
        }
        var required = new[] { 1, 5, 10, 15, 20, 25 };
        for (var id = 0; id < 6; ++id)
        {
            Check(MiniGameProgression.BackLevel(id) == required[id], "Wrong back threshold");
            var xp = MiniGameProgression.ExperienceAtLevel(required[id]);
            Check(MiniGameProgression.IsUnlocked(id, xp), "Boundary not unlocked");
            if (id > 0) Check(!MiniGameProgression.IsUnlocked(id, xp - 1), "Early unlock");
        }
        Check(!MiniGameProgression.IsBack(-1) && !MiniGameProgression.IsBack(6), "Invalid catalog accepted");
    });
    foreach (var durable in new[] { false, true })
    {
        var tag = durable ? "SQLite" : "memory";
        IMiniGameProgressStore NewStore(string name) => durable ? new SqliteMiniGameProgressStore(Path.Combine(root, name + ".db")) : new MemoryMiniGameProgressStore();
        Run(tag + ": character and mini-game isolation", () =>
        {
            var store = NewStore(tag + "-isolation"); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var table = Guid.NewGuid();
            store.AwardWin(a, "poker", table, 1);
            Check(store.Load(a, "poker").Experience == 25 && store.Load(a, "blackjack").Experience == 0 && store.Load(b, "poker").Experience == 0, "Profile bleed");
            store.AwardWin(a, "blackjack", table, 1);
            Check(store.Load(a, "poker").Wins == 1 && store.Load(a, "blackjack").Wins == 1, "Receipt game key collision");
        });
        Run(tag + ": replayed wins remain idempotent", () =>
        {
            var store = NewStore(tag + "-replay"); var a = Guid.NewGuid(); var table = Guid.NewGuid();
            Parallel.For(0, 24, _ => store.AwardWin(a, "poker", table, 1));
            Check(store.Load(a, "poker") == new MiniGameProgress(25, 1, 0), "Repeated reward");
            store.AwardWin(a, "poker", table, 2);
            Check(store.Load(a, "poker").Experience == 50, "New hand suppressed");
        });
        Run(tag + ": locked selection rejected and unlocked selection saved", () =>
        {
            var store = NewStore(tag + "-selection"); var a = Guid.NewGuid(); var table = Guid.NewGuid();
            Throws<ArgumentOutOfRangeException>(() => store.SelectBack(a, "poker", 1));
            Check(store.Load(a, "poker").SelectedBack == 0, "Locked selection mutated profile");
            for (var h = 1; h <= 40; ++h) store.AwardWin(a, "poker", table, h);
            Check(store.Load(a, "poker").Level == 5, "Wrong level at 40 wins");
            store.SelectBack(a, "poker", 1);
            Check(store.Load(a, "poker").SelectedBack == 1, "Selection not saved");
            Throws<ArgumentOutOfRangeException>(() => store.SelectBack(a, "poker", 5));
            Check(store.Load(a, "poker").SelectedBack == 1, "Failed choice erased saved choice");
        });
        Run(tag + ": invalid identities and receipt keys fail without creating profiles", () =>
        {
            var store = NewStore(tag + "-keys"); var a = Guid.NewGuid();
            Throws<ArgumentException>(() => store.Load(Guid.Empty, "poker"));
            Throws<ArgumentException>(() => store.Load(a, "../poker"));
            Throws<ArgumentException>(() => store.AwardWin(a, "poker", Guid.Empty, 1));
            Throws<ArgumentException>(() => store.AwardWin(a, "poker", Guid.NewGuid(), 0));
            Check(store.Load(a, "poker").Experience == 0, "Invalid request gained XP");
        });
    }
    Run("SQLite: XP, choice and receipts survive a new store instance", () =>
    {
        var path = Path.Combine(root, "restart.db"); var a = Guid.NewGuid(); var table = Guid.NewGuid();
        var store = new SqliteMiniGameProgressStore(path);
        for (var h = 1; h <= 40; ++h) store.AwardWin(a, "poker", table, h);
        store.SelectBack(a, "poker", 1);
        var reopened = new SqliteMiniGameProgressStore(path); reopened.AwardWin(a, "poker", table, 40);
        Check(reopened.Load(a, "poker") == new MiniGameProgress(1000, 40, 1), "Restart reset or replayed progress");
    });
    Run("SQLite: XP write failure rolls receipt back in the same transaction", () =>
    {
        var path = Path.Combine(root, "rollback.db"); var a = Guid.NewGuid(); var table = Guid.NewGuid();
        var store = new SqliteMiniGameProgressStore(path); store.Load(a, "poker");
        Sql(path, "CREATE TRIGGER FailProfile BEFORE INSERT ON MiniGameProfiles BEGIN SELECT RAISE(FAIL, 'simulated storage failure'); END;");
        Throws<SqliteException>(() => store.AwardWin(a, "poker", table, 1));
        Sql(path, "DROP TRIGGER FailProfile;"); store.AwardWin(a, "poker", table, 1);
        Check(store.Load(a, "poker") == new MiniGameProgress(25, 1, 0), "XP and receipt were not atomic");
    });
    Run("SQLite: concurrent store instances cannot duplicate the same hand", () =>
    {
        var path = Path.Combine(root, "concurrent.db"); var a = Guid.NewGuid(); var table = Guid.NewGuid();
        var first = new SqliteMiniGameProgressStore(path); var second = new SqliteMiniGameProgressStore(path);
        first.Load(a, "poker");
        Parallel.For(0, 12, i => (i % 2 == 0 ? first : second).AwardWin(a, "poker", table, 1));
        Check(first.Load(a, "poker").Wins == 1, "Concurrent receipt duplicated");
    });
    Run("SQLite: invalid saved unlock is rejected, not silently reset", () =>
    {
        var path = Path.Combine(root, "corrupt.db"); var a = Guid.NewGuid(); var store = new SqliteMiniGameProgressStore(path);
        store.Load(a, "poker");
        Sql(path, $"INSERT INTO MiniGameProfiles VALUES('{a:N}','poker',0,0,5);");
        Throws<InvalidDataException>(() => store.Load(a, "poker"));
        Throws<InvalidDataException>(() => store.AwardWin(a, "poker", Guid.NewGuid(), 1));
    });
    Run("SQLite: inaccessible directory fails closed", () =>
    {
        var path = Path.Combine(root, "not-a-directory"); File.WriteAllText(path, "keep this file");
        var store = new SqliteMiniGameProgressStore(Path.Combine(path, "profile.db"));
        Throws<IOException>(() => store.Load(Guid.NewGuid(), "poker"));
        Check(File.ReadAllText(path) == "keep this file", "Existing data overwritten");
    });
    Run("XP cap and win count remain valid after mastery", () =>
    {
        var store = new MemoryMiniGameProgressStore(); var a = Guid.NewGuid(); var table = Guid.NewGuid();
        for (var h = 1; h <= 1300; ++h) store.AwardWin(a, "poker", table, h);
        var p = store.Load(a, "poker");
        Check(p.Experience == 30000 && p.Level == 25 && p.Wins == 1300 && p.IsValid, "Mastery cap invalid");
        store.SelectBack(a, "poker", 5); Check(store.Load(a, "poker").SelectedBack == 5, "B6 not unlocked");
    });
    Run("Registry: unreadable profile never grants a temporary level-one seat", () =>
    {
        var store = new FaultStore { FailLoad = true }; var registry = new PokerTableRegistry(store);
        var p = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), Guid.Empty);
        Check(registry.Join(p, "broken", "Player", new()).Error == PokerRegistryError.ProgressionUnavailable, "Load error ignored");
        Check(registry.MemberCount == 0 && registry.TableCount == 0, "Failed load mutated table");
    });
    Run("Registry: uncertain commit retries the original receipt without XP duplication", () =>
    {
        var store = new FaultStore { ThrowAfterAward = true }; var registry = new PokerTableRegistry(store);
        var map = Guid.NewGuid(); var a = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
        var b = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
        var id = registry.Join(a, "retry", "Alice", new PokerRules(2)).TableInstanceId;
        registry.Join(b, "retry", "Bob", new PokerRules(2));
        var now = DateTimeOffset.UtcNow;
        var hand = registry.StartHand(a, id, registry.Snapshot(a, id).Snapshot!.Revision, now).Snapshot!;
        registry.Act(a, id, hand.HandId, hand.Revision, PokerAction.Fold, 0, now);
        Check(registry.Presentation(b, id).ProgressPending, "Storage failure not surfaced");
        Check(registry.StartHand(b, id, registry.Snapshot(b, id).Snapshot!.Revision, now).Error == PokerRegistryError.ProgressionUnavailable, "New hand bypassed failed save");
        registry.Tick(now.AddSeconds(5), _ => true);
        var profile = registry.Presentation(b, id);
        Check(profile.Experience == 25 && profile.Wins == 1 && !profile.ProgressPending, "Retry doubled or lost XP");
    });
}
finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
Console.WriteLine($"{passed}/{passed + failed} mini-game persistence groups passed.");
Environment.ExitCode = failed == 0 ? 0 : 1;
void Run(string name, Action test)
{ try { test(); ++passed; Console.WriteLine("PASS PROGRESS: " + name); } catch (Exception ex) { ++failed; Console.Error.WriteLine("FAIL PROGRESS: " + name + "\n" + ex); } }
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
static void Sql(string path, string text)
{ using var c = new SqliteConnection($"Data Source={path};Pooling=False"); c.Open(); using var q = c.CreateCommand(); q.CommandText = text; q.ExecuteNonQuery(); }
sealed class FaultStore : IMiniGameProgressStore
{
    private readonly MemoryMiniGameProgressStore _inner = new();
    public bool FailLoad, ThrowAfterAward;
    public MiniGameProgress Load(Guid id, string game) => FailLoad ? throw new IOException("test load failure") : _inner.Load(id, game);
    public MiniGameProgress SelectBack(Guid id, string game, int back) => _inner.SelectBack(id, game, back);
    public MiniGameProgress AwardWin(Guid id, string game, Guid table, long hand)
    {
        var p = _inner.AwardWin(id, game, table, hand);
        if (ThrowAfterAward) { ThrowAfterAward = false; throw new IOException("test uncertain commit"); }
        return p;
    }
}
