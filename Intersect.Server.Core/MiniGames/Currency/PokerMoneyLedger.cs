#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.MiniGames.Currency;

public sealed record MoneySeat(Guid Id, Guid Table, Guid Character, Guid Currency, string House, long Amount, bool Npc, int Status);
public sealed record MoneyWin(Guid Character, long Net);
public sealed class MoneyRuleException(string message) : Exception(message);

/// <summary>
/// Inventory and escrow share the PLAYER SQLite database and the SAME transaction.
/// Checkpoints represent completed hands. An unfinished hand is voided for everyone on restart.
/// An OS lease prevents a second game-server process from recovering a live owner's tables.
/// </summary>
public sealed partial class PokerMoneyLedger : IDisposable
{
    public const long MaximumBalance = 1_000_000_000_000;
    private readonly object _gate = new();
    private readonly string _connectionString;
    private readonly FileStream _lease;
    private readonly string _boot = Guid.NewGuid().ToString("N");
    private bool _disposed;
    internal Action<string>? Fault;
    internal Action<Exception>? UnverifiableCommit;
    public PokerMoneyLedger(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
            throw new NotSupportedException("Funded poker requires a file-backed player SQLite database.");
        builder.DataSource = Path.GetFullPath(builder.DataSource);
        builder.Mode = SqliteOpenMode.ReadWrite; builder.Pooling = false; builder.DefaultTimeout = 2;
        _connectionString = builder.ToString();
        _lease = new FileStream(builder.DataSource + ".poker.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            using var c = Open(); using var tx = c.BeginTransaction();
            Exec(c, tx, """
                CREATE TABLE IF NOT EXISTS PokerMoneyHouses (
                    House TEXT PRIMARY KEY, Currency TEXT NOT NULL,
                    Seed INTEGER NOT NULL CHECK(Seed >= 0), Available INTEGER NOT NULL CHECK(Available >= 0));
                CREATE TABLE IF NOT EXISTS PokerMoneySeats (
                    Id TEXT PRIMARY KEY, TableId TEXT NOT NULL, CharacterId TEXT NOT NULL,
                    Currency TEXT NOT NULL, House TEXT NOT NULL, Boot TEXT NOT NULL,
                    Amount INTEGER NOT NULL CHECK(Amount BETWEEN 0 AND 1000000000000),
                    Npc INTEGER NOT NULL CHECK(Npc IN (0,1)), Status INTEGER NOT NULL CHECK(Status IN (0,1,2)));
                CREATE UNIQUE INDEX IF NOT EXISTS PokerMoneyOneActiveSeat
                    ON PokerMoneySeats(CharacterId) WHERE Npc=0 AND Status=0;
                CREATE INDEX IF NOT EXISTS PokerMoneyRefunds ON PokerMoneySeats(CharacterId,Status);
                CREATE TABLE IF NOT EXISTS PokerMoneyTransfers (
                    Receipt TEXT PRIMARY KEY, CharacterId TEXT NOT NULL, Currency TEXT NOT NULL,
                    Amount INTEGER NOT NULL CHECK(Amount >= 0), Kind TEXT NOT NULL, Created INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS PokerMoneyHands (
                    TableId TEXT NOT NULL, HandId INTEGER NOT NULL, Fingerprint TEXT NOT NULL,
                    Created INTEGER NOT NULL, PRIMARY KEY(TableId,HandId));
                CREATE TABLE IF NOT EXISTS PokerMoneyProfiles (
                    CharacterId TEXT NOT NULL, Game TEXT NOT NULL,
                    Experience INTEGER NOT NULL CHECK(Experience BETWEEN 0 AND 30000),
                    Wins INTEGER NOT NULL CHECK(Wins >= 0), SelectedBack INTEGER NOT NULL CHECK(SelectedBack BETWEEN 0 AND 5),
                    PRIMARY KEY(CharacterId,Game));
                """);
            foreach (var s in Seats(c, tx, "Status=0 AND Npc=1")) ReturnNpc(c, tx, s);
            Exec(c, tx, "UPDATE PokerMoneySeats SET Status=1 WHERE Status=0 AND Npc=0;");
            tx.Commit();
        }
        catch { _lease.Dispose(); throw; }
    }
    private SqliteConnection Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var c = new SqliteConnection(_connectionString);
        try { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA synchronous=FULL;"; cmd.ExecuteNonQuery(); return c; }
        catch { c.Dispose(); throw; }
    }
    private static string Id(Guid id) => id.ToString("N");
    private static void Required(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Missing identity."); }
    private static void Amount(long n) { if (n < 0 || n > MaximumBalance) throw new MoneyRuleException("Amount outside the supported range."); }
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? tx, string sql, params object[] values)
    {
        var command = c.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        for (var i = 0; i < values.Length; ++i) command.Parameters.AddWithValue("$p" + i, values[i]);
        return command;
    }
    private static int Exec(SqliteConnection c, SqliteTransaction tx, string sql, params object[] values)
    { using var command = Command(c, tx, sql, values); return command.ExecuteNonQuery(); }
    private static object? Scalar(SqliteConnection c, SqliteTransaction? tx, string sql, params object[] values)
    { using var command = Command(c, tx, sql, values); return command.ExecuteScalar(); }
    private static MoneySeat[] Seats(SqliteConnection c, SqliteTransaction? tx, string where, params object[] values)
    {
        using var cmd = Command(c, tx, "SELECT Id,TableId,CharacterId,Currency,House,Amount,Npc,Status FROM PokerMoneySeats WHERE " + where, values);
        using var reader = cmd.ExecuteReader(); var result = new List<MoneySeat>();
        while (reader.Read()) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)), Guid.Parse(reader.GetString(3)), reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6) != 0, reader.GetInt32(7)));
        return result.ToArray();
    }
    private void InsertSeat(SqliteConnection c, SqliteTransaction tx, MoneySeat s) => Exec(c, tx,
        "INSERT INTO PokerMoneySeats VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,0);",
        Id(s.Id), Id(s.Table), Id(s.Character), Id(s.Currency), s.House, _boot, s.Amount, s.Npc ? 1 : 0);
    private T Write<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    { lock (_gate) { using var c = Open(); using var tx = c.BeginTransaction(); var result = work(c, tx); tx.Commit(); return result; } }
    private static bool HasReceipt(SqliteConnection c, string receipt, Guid character, Guid currency, long amount, string kind)
    {
        using var cmd = Command(c, null, "SELECT CharacterId,Currency,Amount,Kind FROM PokerMoneyTransfers WHERE Receipt=$p0", receipt);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;
        if (reader.GetString(0) != Id(character) || reader.GetString(1) != Id(currency) || reader.GetInt64(2) != amount || reader.GetString(3) != kind)
            throw new MoneyRuleException("Transfer receipt payload mismatch.");
        return true;
    }
    private static void Receipt(SqliteConnection c, SqliteTransaction tx, string receipt, Guid character, Guid currency, long amount, string kind) =>
        Exec(c, tx, "INSERT INTO PokerMoneyTransfers VALUES($p0,$p1,$p2,$p3,$p4,$p5)",
            receipt, Id(character), Id(currency), amount, kind, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    // No fallible I/O may occur after this returns and before live inventory synchronization.
    // Caller holds EntityLock and the actual User.Save lock across this entire operation.
    private void Transfer(string receipt, Guid character, Guid currency, long amount, string kind, Action<SqliteConnection, SqliteTransaction> work)
    {
        Required(character); Required(currency); Amount(amount);
        lock (_gate)
        {
            using var c = Open();
            if (HasReceipt(c, receipt, character, currency, amount, kind)) return;
            using var tx = c.BeginTransaction(); var attempted = false;
            try { work(c, tx); Fault?.Invoke("before-commit"); attempted = true; tx.Commit(); Fault?.Invoke("after-commit"); }
            catch (Exception error)
            {
                try { tx.Rollback(); } catch { }
                if (attempted)
                {
                    try { using var check = Open(); if (HasReceipt(check, receipt, character, currency, amount, kind)) return; }
                    catch (Exception verificationError)
                    {
                        var fatal = new AggregateException("Cannot resolve a poker inventory commit; restart is required.", error, verificationError);
                        if (UnverifiableCommit != null) UnverifiableCommit(fatal); else Environment.FailFast(fatal.Message, fatal);
                        throw fatal;
                    }
                }
                throw;
            }
        }
    }
    public MoneySeat OpenHuman(Guid seat, Guid table, Guid character, Guid currency, string house, long amount, Action<SqliteConnection, SqliteTransaction> debit)
    {
        Required(seat); Required(table); Required(character); Required(currency); Amount(amount);
        if (amount == 0 || string.IsNullOrWhiteSpace(house) || house.Length > 200) throw new MoneyRuleException("Invalid buy-in.");
        var expected = new MoneySeat(seat, table, character, currency, house, amount, false, 0);
        var kind = "buy-in:" + Id(table) + ":" + house;
        Transfer(Id(seat) + ":in", character, currency, amount, kind, (c, tx) =>
        { InsertSeat(c, tx, expected); debit(c, tx); Receipt(c, tx, Id(seat) + ":in", character, currency, amount, kind); });
        return expected;
    }
    public MoneySeat? OpenNpc(Guid seat, Guid table, Guid currency, string house, long seed, long stake)
    {
        Required(seat); Required(table); Required(currency); Amount(seed); Amount(stake);
        if (stake == 0 || string.IsNullOrWhiteSpace(house) || house.Length > 200) throw new MoneyRuleException("Invalid NPC funding.");
        return Write<MoneySeat?>((c, tx) =>
        {
            var existing = Seats(c, tx, "Id=$p0", Id(seat)).SingleOrDefault();
            if (existing != null)
            {
                if (!existing.Npc || existing.Status != 0 || existing.Table != table || existing.Currency != currency || existing.House != house)
                    throw new MoneyRuleException("NPC admission receipt mismatch.");
                return existing;
            }
            var previous = Scalar(c, tx, "SELECT Available FROM PokerMoneyHouses WHERE House=$p0 AND Currency=$p1", house, Id(currency));
            if (previous == null && seed == 0) return null;
            Exec(c, tx, "INSERT INTO PokerMoneyHouses VALUES($p0,$p1,$p2,$p2) ON CONFLICT(House) DO NOTHING;", house, Id(currency), seed);
            var available = Convert.ToInt64(Scalar(c, tx, "SELECT Available FROM PokerMoneyHouses WHERE House=$p0 AND Currency=$p1", house, Id(currency))
                ?? throw new MoneyRuleException("Conflicting house currency."));
            if (available < stake) return null;
            Exec(c, tx, "UPDATE PokerMoneyHouses SET Available=Available-$p1 WHERE House=$p0", house, stake);
            var result = new MoneySeat(seat, table, Guid.Empty, currency, house, stake, true, 0); InsertSeat(c, tx, result); return result;
        });
    }
    public long HouseAvailable(string house)
    { lock (_gate) { using var c = Open(); return Convert.ToInt64(Scalar(c, null, "SELECT Available FROM PokerMoneyHouses WHERE House=$p0", house) ?? 0L); } }
    private static void ReturnNpc(SqliteConnection c, SqliteTransaction tx, MoneySeat s)
    {
        if (!s.Npc || s.Status == 2) return;
        if (Exec(c, tx, "UPDATE PokerMoneyHouses SET Available=Available+$p1 WHERE House=$p0 AND Currency=$p2 AND Available<=$p3",
            s.House, s.Amount, Id(s.Currency), MaximumBalance - s.Amount) != 1) throw new MoneyRuleException("Invalid house refund.");
        Exec(c, tx, "UPDATE PokerMoneySeats SET Status=2 WHERE Id=$p0", Id(s.Id));
    }
    public void Release(Guid seat) => Write((c, tx) =>
    {
        var s = Seats(c, tx, "Id=$p0", Id(seat)).SingleOrDefault() ?? throw new MoneyRuleException("Unknown escrow.");
        if (s.Npc) ReturnNpc(c, tx, s); else Exec(c, tx, "UPDATE PokerMoneySeats SET Status=1 WHERE Id=$p0 AND Status=0", Id(seat));
        return true;
    });
    // Only call when EntityLock is held and no runtime membership owns this character.
    public void ReleaseOrphanHuman(Guid character) => Write((c, tx) =>
    { Required(character); return Exec(c, tx, "UPDATE PokerMoneySeats SET Status=1 WHERE CharacterId=$p0 AND Npc=0 AND Status=0", Id(character)); });
    public MoneySeat[] Refunds(Guid character)
    { Required(character); lock (_gate) { using var c = Open(); return Seats(c, null, "CharacterId=$p0 AND Npc=0 AND Status=1", Id(character)); } }
    public void Cashout(MoneySeat expected, Action<SqliteConnection, SqliteTransaction> credit)
    {
        if (expected.Npc) throw new MoneyRuleException("NPC credits cannot enter an inventory.");
        Transfer(Id(expected.Id) + ":out", expected.Character, expected.Currency, expected.Amount, "cash-out", (c, tx) =>
        {
            var s = Seats(c, tx, "Id=$p0", Id(expected.Id)).SingleOrDefault();
            if (s == null || s.Npc || s.Status != 1 || s.Amount != expected.Amount || s.Character != expected.Character || s.Currency != expected.Currency)
                throw new MoneyRuleException("Escrow changed or is not withdrawable.");
            if (s.Amount > 0) credit(c, tx);
            Exec(c, tx, "UPDATE PokerMoneySeats SET Status=2 WHERE Id=$p0", Id(s.Id));
            Receipt(c, tx, Id(s.Id) + ":out", s.Character, s.Currency, s.Amount, "cash-out");
        });
    }
    public MoneyWin[] Settle(Guid table, long hand, IReadOnlyDictionary<Guid, long> closing)
    {
        Required(table); if (hand <= 0 || closing.Count < 2 || closing.Count > 6) throw new MoneyRuleException("Invalid settlement.");
        foreach (var value in closing.Values) Amount(value);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",
            closing.OrderBy(p => p.Key).Select(p => Id(p.Key) + ":" + p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))))));
        return Write((c, tx) =>
        {
            var old = Scalar(c, tx, "SELECT Fingerprint FROM PokerMoneyHands WHERE TableId=$p0 AND HandId=$p1", Id(table), hand) as string;
            if (old != null) { if (old != fingerprint) throw new MoneyRuleException("A settled hand cannot change its result."); return Array.Empty<MoneyWin>(); }
            var seats = Seats(c, tx, "TableId=$p0 AND Status=0", Id(table));
            if (seats.Length != closing.Count || seats.Any(s => !closing.ContainsKey(s.Id)) || seats.Select(s => s.Currency).Distinct().Count() != 1 ||
                seats.Sum(s => s.Amount) != closing.Values.Sum()) throw new MoneyRuleException("Settlement does not conserve funded currency.");
            var wins = new List<MoneyWin>();
            foreach (var s in seats)
            {
                var amount = closing[s.Id]; Exec(c, tx, "UPDATE PokerMoneySeats SET Amount=$p1 WHERE Id=$p0", Id(s.Id), amount);
                if (!s.Npc && amount > s.Amount)
                { WriteProfile(c, tx, s.Character, ReadProfile(c, tx, s.Character).WithWin()); wins.Add(new(s.Character, amount - s.Amount)); }
            }
            Exec(c, tx, "INSERT INTO PokerMoneyHands VALUES($p0,$p1,$p2,$p3)", Id(table), hand, fingerprint, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Fault?.Invoke("before-settlement-commit"); return wins.ToArray();
        });
    }
    private static MiniGameProgress ReadProfile(SqliteConnection c, SqliteTransaction? tx, Guid character)
    {
        using var cmd = Command(c, tx, "SELECT Experience,Wins,SelectedBack FROM PokerMoneyProfiles WHERE CharacterId=$p0 AND Game='poker'", Id(character));
        using var reader = cmd.ExecuteReader();
        var result = reader.Read() ? new MiniGameProgress(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2)) : new();
        if (!result.IsValid) throw new MoneyRuleException("Invalid funded progression; refusing to reset it."); return result;
    }
    private static void WriteProfile(SqliteConnection c, SqliteTransaction tx, Guid character, MiniGameProgress p) => Exec(c, tx,
        "INSERT INTO PokerMoneyProfiles VALUES($p0,'poker',$p1,$p2,$p3) ON CONFLICT(CharacterId,Game) DO UPDATE SET Experience=$p1,Wins=$p2,SelectedBack=$p3;",
        Id(character), p.Experience, p.Wins, p.SelectedBack);
    public MiniGameProgress Profile(Guid character)
    { Required(character); lock (_gate) { using var c = Open(); return ReadProfile(c, null, character); } }
    public MiniGameProgress SelectBack(Guid character, int back) => Write((c, tx) =>
    {
        Required(character); var p = ReadProfile(c, tx, character);
        if (!MiniGameProgression.IsUnlocked(back, p.Experience)) throw new MoneyRuleException("CardBackLocked");
        p = p with { SelectedBack = back }; WriteProfile(c, tx, character, p); return p;
    });
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _lease.Dispose(); } }
}
