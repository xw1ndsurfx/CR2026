using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Microsoft.Data.Sqlite;

var passed = 0;
var failed = 0;
void Test(string name, Action<Fixture> run)
{
    using var f = new Fixture();
    try { run(f); ++passed; Console.WriteLine("PASS funded poker: " + name); }
    catch (Exception error) { ++failed; Console.Error.WriteLine("FAIL funded poker: " + name + "\n" + error); }
}
Test("Buy-in uses the selected inventory currency, not a free starting stack", f =>
{
    var s = f.Buy(f.A, 100); Check(f.Balance(f.A) == 250 && f.Amount(s.Id) == 100, "Debit/escrow mismatch");
    Check(f.Balance(f.A, f.OtherCurrency) == 777, "Unrelated currency changed");
});
Test("Insufficient funds rolls back both inventory and escrow", f =>
{
    Throws(() => f.Buy(f.A, 400)); Check(f.Balance(f.A) == 350 && f.Count("PokerMoneySeats") == 0, "Partial debit");
});
Test("Repeated buy-in receipt never calls inventory twice", f =>
{
    var s = f.Buy(f.A, 100);
    f.Money.OpenHuman(s.Id, s.Table, s.Character, s.Currency, s.House, 100, (_, _) => throw new Exception("Replayed debit"));
    Check(f.Balance(f.A) == 250 && f.Count("PokerMoneyTransfers") == 1, "Duplicate debit");
});
Test("Pre-commit failure rolls back inventory and allows retry", f =>
{
    f.Money.Fault = phase => { if (phase == "before-commit") throw new IOException("disk fault"); };
    Throws(() => f.Buy(f.A, 100));
    Check(f.Balance(f.A) == 350 && f.Count("PokerMoneySeats") == 0, "Rollback failed");
    f.Money.Fault = null; f.Buy(f.A, 100); Check(f.Balance(f.A) == 250, "Retry failed");
});
Test("Lost commit acknowledgement is resolved by the durable receipt", f =>
{
    f.Money.Fault = phase => { if (phase == "after-commit") throw new IOException("lost acknowledgement"); };
    var s = f.Buy(f.A, 100);
    Check(f.Balance(f.A) == 250 && f.Amount(s.Id) == 100, "Committed debit not recovered");
});
Test("A character cannot pay for a second active seat", f =>
{
    f.Buy(f.A, 100); Throws(() => f.Buy(f.A, 100)); Check(f.Balance(f.A) == 250, "Second active debit");
});
Test("All player balances, XP and the hand receipt settle together", f =>
{
    var a = f.Buy(f.A, 100); var b = f.Buy(f.B, 100);
    f.Money.Settle(f.TableId, 1, new Dictionary<Guid,long> { [a.Id] = 125, [b.Id] = 75 });
    Check(f.Amount(a.Id) == 125 && f.Amount(b.Id) == 75, "Settlement balances");
    Check(f.Money.Profile(f.A).Experience == 25 && f.Money.Profile(f.B).Experience == 0, "Net-win XP");
    f.Pay(a); f.Pay(b); Check(f.Balance(f.A) == 375 && f.Balance(f.B) == 325, "Final inventory");
});
Test("Settlement replay cannot duplicate XP or change the result", f =>
{
    var a = f.Buy(f.A, 100); var b = f.Buy(f.B, 100);
    var result = new Dictionary<Guid,long> { [a.Id] = 150, [b.Id] = 50 };
    f.Money.Settle(f.TableId, 1, result); f.Money.Settle(f.TableId, 1, result);
    Check(f.Money.Profile(f.A).Experience == 25 && f.Money.Profile(f.A).Wins == 1, "Repeated XP");
    result[a.Id] = 160; result[b.Id] = 40;
    Throws(() => f.Money.Settle(f.TableId, 1, result)); Check(f.Amount(a.Id) == 150, "Result mutated");
});
Test("Unbalanced settlement is rejected before changing any balance", f =>
{
    var a = f.Buy(f.A, 100); var b = f.Buy(f.B, 100);
    Throws(() => f.Money.Settle(f.TableId, 1, new Dictionary<Guid,long> { [a.Id] = 150, [b.Id] = 100 }));
    Check(f.Amount(a.Id) == 100 && f.Money.Profile(f.A).Experience == 0, "Currency minted");
});
Test("Failed hand commit keeps the whole previous checkpoint", f =>
{
    var a = f.Buy(f.A, 100); var b = f.Buy(f.B, 100);
    f.Money.Fault = phase => { if (phase == "before-settlement-commit") throw new IOException("fault"); };
    Throws(() => f.Money.Settle(f.TableId, 1, new Dictionary<Guid,long> { [a.Id] = 130, [b.Id] = 70 }));
    Check(f.Amount(a.Id) == 100 && f.Amount(b.Id) == 100 && f.Money.Profile(f.A).Experience == 0, "Partial settlement");
});
Test("An unfinished hand is voided for everyone after restart", f =>
{
    f.Buy(f.A, 100); f.Buy(f.B, 100); f.Restart();
    Check(f.Money.Refunds(f.A).Single().Amount == 100 && f.Money.Refunds(f.B).Single().Amount == 100, "Unequal recovery");
    f.RefundAll(f.A); f.RefundAll(f.B); Check(f.Balance(f.A) == 350 && f.Balance(f.B) == 350, "Lost buy-in");
});
Test("A committed result and its XP survive restart", f =>
{
    var a = f.Buy(f.A, 100); var b = f.Buy(f.B, 100);
    f.Money.Settle(f.TableId, 1, new Dictionary<Guid,long> { [a.Id] = 125, [b.Id] = 75 });
    f.Restart(); f.RefundAll(f.A); f.RefundAll(f.B);
    Check(f.Balance(f.A) == 375 && f.Balance(f.B) == 325 && f.Money.Profile(f.A).Experience == 25, "Result lost");
});
Test("A full inventory keeps an unpaid claim instead of dropping it", f =>
{
    var a = f.Buy(f.A, 100); f.Money.Release(a.Id); var claim = f.Money.Refunds(f.A).Single();
    Throws(() => f.Money.Cashout(claim, (_, _) => throw new MoneyRuleException("full")));
    Check(f.Money.Refunds(f.A).Single().Amount == 100 && f.Balance(f.A) == 250, "Claim lost");
    f.RefundAll(f.A); Check(f.Balance(f.A) == 350, "Deferred refund failed");
});
Test("Concurrent repeated cash-out requests credit once", f =>
{
    var a = f.Buy(f.A, 100); f.Money.Release(a.Id); var claim = f.Money.Refunds(f.A).Single();
    Parallel.For(0, 12, _ => f.Money.Cashout(claim, (c,t) => f.Adjust(c,t,f.A,f.Currency,100)));
    Check(f.Balance(f.A) == 350 && f.Money.Refunds(f.A).Length == 0, "Duplicate refund");
});
Test("Cash-out commit acknowledgement loss does not duplicate credit", f =>
{
    var a = f.Buy(f.A, 100); f.Money.Release(a.Id);
    f.Money.Fault = phase => { if (phase == "after-commit") throw new IOException("ack lost"); };
    f.RefundAll(f.A); f.Restart(); f.RefundAll(f.A); Check(f.Balance(f.A) == 350, "Duplicate after restart");
});
Test("NPC funding is finite and shared across instances", f =>
{
    var npc = f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "same-house", 100, 100)!;
    Check(f.Money.HouseAvailable("same-house") == 0, "Loan was not reserved");
    Check(f.Money.OpenNpc(Guid.NewGuid(), Guid.NewGuid(), f.Currency, "same-house", 999999, 100) == null, "Another instance reseeded");
    f.Money.Release(npc.Id); Check(f.Money.HouseAvailable("same-house") == 100, "NPC exit lost funds");
});
Test("Winning NPC money depletes the house instead of minting refills", f =>
{
    var a = f.Buy(f.A, 100);
    var npc = f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "finite", 100, 100)!;
    f.Money.Settle(f.TableId, 1, new Dictionary<Guid,long> { [a.Id] = 200, [npc.Id] = 0 });
    f.Money.Release(npc.Id); f.Restart();
    Check(f.Money.OpenNpc(Guid.NewGuid(), Guid.NewGuid(), f.Currency, "finite", 100000, 100) == null, "Busted NPC refilled for free");
    f.RefundAll(f.A); Check(f.Balance(f.A) == 450, "Winning cash-out");
});
Test("House loans are returned on recovery and do not seed twice", f =>
{
    f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "house", 1000, 100);
    f.Restart(); Check(f.Money.HouseAvailable("house") == 1000, "Recovery lost NPC stake");
    f.Restart(); Check(f.Money.HouseAvailable("house") == 1000, "Recovery duplicated NPC stake");
});
Test("Unfunded initial configuration does not consume the future seed", f =>
{
    Check(f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "unfunded", 0, 100) == null, "Unexpected NPC");
    Check(f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "unfunded", 1000, 100) != null, "Zero permanently consumed seed");
});
Test("The OS lease prevents a second simultaneous recovery owner", f =>
{
    Throws(() => { using var duplicate = new PokerMoneyLedger(f.ConnectionString); });
});
Test("Locked cosmetics cannot be equipped and selected B1 persists", f =>
{
    Throws(() => f.Money.SelectBack(f.A, 5)); f.Money.SelectBack(f.A, 0); f.Restart();
    Check(f.Money.Profile(f.A).SelectedBack == 0 && f.Money.Profile(f.A).Experience == 0, "Cosmetic progression");
});
Test("Two humans play the actual engine using funded bankrolls", f =>
{
    var table = f.NewTable(); var a = f.Buy(f.A, 100, table.Id, table.House); var b = f.Buy(f.B, 100, table.Id, table.House);
    table.Join(a,"Alice"); table.Join(b,"Bob"); var now = DateTimeOffset.UtcNow;
    Check(table.Start(f.A, table.Snapshot(f.A).Revision, now) == PokerError.None, "Start failed");
    var state = table.Snapshot(f.A); var actor = state.Seats.Single(s => s.Seat == state.ActingSeat).PlayerId;
    Check(state.Seats.All(s => s.RevealedCards.Length == 0), "Private cards leaked");
    table.Act(actor, state.HandId, state.Revision, PokerAction.Fold, 0, now.AddMilliseconds(10));
    state = table.Snapshot(f.A); Check(state.Phase == PokerPhase.Finished && state.Seats.Sum(s => s.Chips) == 200, "Hand failed");
    Check(f.Money.Profile(f.A).Experience + f.Money.Profile(f.B).Experience == 25, "Net-win XP missing");
    table.Leave(f.A,now); table.Leave(f.B,now); f.RefundAll(f.A); f.RefundAll(f.B);
    Check(table.IsEmpty && f.Balance(f.A) + f.Balance(f.B) == 700, "Funded bankroll not conserved");
});
Test("Leaving mid-hand does not cash out an unsettled wager", f =>
{
    var table = f.NewTable(); var a = f.Buy(f.A,100,table.Id,table.House); var b = f.Buy(f.B,100,table.Id,table.House);
    table.Join(a,"Alice"); table.Join(b,"Bob"); var now = DateTimeOffset.UtcNow;
    table.Start(f.A,table.Snapshot(f.A).Revision,now);
    var state = table.Snapshot(f.A); var acting = state.Seats.Single(s => s.Seat == state.ActingSeat).PlayerId;
    var waiting = acting == f.A ? f.B : f.A;
    table.Leave(waiting,now);
    Check(f.Money.Refunds(waiting).Length == 0, "Early payout");
    state = table.Snapshot(acting);
    table.Act(acting,state.HandId,state.Revision,PokerAction.Call,0,now.AddMilliseconds(1));
    table.Tick(now.AddSeconds(1));
    Check(table.Snapshot(acting).Phase == PokerPhase.Finished, "Leaving hand did not settle");
    table.Leave(acting,now.AddSeconds(2)); f.RefundAll(f.A); f.RefundAll(f.B);
    Check(f.Balance(f.A)+f.Balance(f.B)==700, "Exit lost or minted currency");
});
Test("A late spectator seat may leave without changing the active pot", f =>
{
    var table = f.NewTable(); table.Join(f.Buy(f.A,100,table.Id,table.House),"Alice"); table.Join(f.Buy(f.B,100,table.Id,table.House),"Bob");
    var now = DateTimeOffset.UtcNow; table.Start(f.A,table.Snapshot(f.A).Revision,now);
    var c = f.Account(350); table.Join(f.Buy(c,100,table.Id,table.House),"Charlie");
    Check(table.Snapshot(c).MyCards.Length == 0, "Late join cards"); table.Leave(c,now); f.RefundAll(c); Check(f.Balance(c)==350,"Late join refund");
    var state=table.Snapshot(f.A); var actor=state.Seats.Single(s=>s.Seat==state.ActingSeat).PlayerId;
    table.Act(actor,state.HandId,state.Revision,PokerAction.Fold,0,now.AddMilliseconds(1));
    Check(table.Snapshot(f.A).Phase==PokerPhase.Finished,"Settlement included removed seat");
});
Test("A failed settlement freezes and retries without repeat XP", f =>
{
    var table=f.NewTable(); table.Join(f.Buy(f.A,100,table.Id,table.House),"Alice"); table.Join(f.Buy(f.B,100,table.Id,table.House),"Bob");
    var now=DateTimeOffset.UtcNow; table.Start(f.A,table.Snapshot(f.A).Revision,now); var state=table.Snapshot(f.A);
    var actor=state.Seats.Single(s=>s.Seat==state.ActingSeat).PlayerId;
    f.Money.Fault=phase=> { if(phase=="before-settlement-commit") throw new IOException("fault"); };
    Throws(()=>table.Act(actor,state.HandId,state.Revision,PokerAction.Fold,0,now.AddMilliseconds(1)));
    table.Suspend(now); Check(table.Pending && table.Presentation(f.A).NetWin==0,"Uncommitted win published");
    f.Money.Fault=null; table.Tick(now.AddSeconds(3)); table.Tick(now.AddSeconds(4));
    Check(!table.Pending && f.Money.Profile(f.A).Experience+f.Money.Profile(f.B).Experience==25,"Retry duplicated XP");
});
Test("Named NPCs play only with debited house bankrolls", f =>
{
    var table=f.NewTable(new PokerTableOptions(true,2,false),3000);
    table.Join(f.Buy(f.A,100,table.Id,table.House),"Alice"); var now=DateTimeOffset.UtcNow; table.Tick(now);
    var state=table.Snapshot(f.A); Check(state.Seats.Length==4 && state.Seats.Any(s=>s.Name=="Marlow"),"NPC seats missing");
    Check(f.Money.HouseAvailable(table.House)==2700,"NPC loans not deducted");
    table.Start(f.A,state.Revision,now);
    for(var i=0;i<300 && table.Snapshot(f.A).Phase!=PokerPhase.Finished;++i)
    {
        now=now.AddMilliseconds(500); state=table.Snapshot(f.A);
        if(state.Seats.Single(s=>s.Seat==state.ActingSeat).PlayerId==f.A)
            table.Act(f.A,state.HandId,state.Revision,state.ToCall>0?PokerAction.Call:PokerAction.Check,0,now);
        else table.Tick(now);
    }
    Check(table.Snapshot(f.A).Phase==PokerPhase.Finished,"NPC game stalled");
    table.Leave(f.A,now); f.RefundAll(f.A);
    Check(table.IsEmpty && f.Balance(f.A)+f.Money.HouseAvailable(table.House)==3350,"NPC/human conservation");
});
Console.WriteLine($"{passed}/{passed+failed} funded poker groups passed.");
Environment.ExitCode=failed==0?0:1;
static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
static void Throws(Action action)
{ try { action(); } catch { return; } throw new InvalidOperationException("Expected rejection"); }

internal sealed class Fixture : IDisposable
{
    private readonly string _path=Path.Combine(Path.GetTempPath(),"poker-money-"+Guid.NewGuid()+".db");
    public string ConnectionString { get; }
    public PokerMoneyLedger Money { get; private set; }
    public Guid Currency { get; }=Guid.NewGuid(); public Guid OtherCurrency { get; }=Guid.NewGuid();
    public Guid TableId { get; }=Guid.NewGuid(); public Guid A { get; }=Guid.NewGuid(); public Guid B { get; }=Guid.NewGuid();
    public Fixture()
    {
        ConnectionString=new SqliteConnectionStringBuilder {DataSource=_path,Pooling=false}.ToString();
        using(var c=Open())
        {
            using var command=c.CreateCommand(); command.CommandText="CREATE TABLE Inventory(Character TEXT,Currency TEXT,Amount INTEGER CHECK(Amount>=0),PRIMARY KEY(Character,Currency));"; command.ExecuteNonQuery();
        }
        Seed(A,Currency,350); Seed(B,Currency,350); Seed(A,OtherCurrency,777);
        Money=new PokerMoneyLedger(ConnectionString);
    }
    private SqliteConnection Open() { var c=new SqliteConnection(ConnectionString); c.Open(); return c; }
    public Guid Account(long amount) { var id=Guid.NewGuid(); Seed(id,Currency,amount); return id; }
    private void Seed(Guid person,Guid currency,long value)
    {
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO Inventory VALUES($a,$c,$v)";
        cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",currency.ToString("N"));cmd.Parameters.AddWithValue("$v",value);cmd.ExecuteNonQuery();
    }
    public void Adjust(SqliteConnection c,SqliteTransaction t,Guid person,Guid currency,long delta)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="UPDATE Inventory SET Amount=Amount+$v WHERE Character=$a AND Currency=$c AND Amount+$v>=0;";
        cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",currency.ToString("N"));cmd.Parameters.AddWithValue("$v",delta);
        if(cmd.ExecuteNonQuery()!=1)throw new MoneyRuleException("Insufficient inventory");
    }
    public MoneySeat Buy(Guid person,long amount,Guid table=default,string house="test-house") =>
        Money.OpenHuman(Guid.NewGuid(),table==Guid.Empty?TableId:table,person,Currency,house,amount,(c,t)=>Adjust(c,t,person,Currency,-amount));
    public void Pay(MoneySeat seat) { Money.Release(seat.Id); RefundAll(seat.Character); }
    public void RefundAll(Guid person)
    { foreach(var s in Money.Refunds(person)) Money.Cashout(s,(c,t)=>Adjust(c,t,person,s.Currency,s.Amount)); }
    public long Balance(Guid person,Guid currency=default)
    {
        using var c=Open(); using var cmd=c.CreateCommand();cmd.CommandText="SELECT Amount FROM Inventory WHERE Character=$a AND Currency=$c";
        cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",(currency==Guid.Empty?Currency:currency).ToString("N"));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
    public long Amount(Guid seat)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Amount FROM PokerMoneySeats WHERE Id=$id";cmd.Parameters.AddWithValue("$id",seat.ToString("N"));return Convert.ToInt64(cmd.ExecuteScalar());
    }
    public long Count(string table)
    { using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM "+table;return Convert.ToInt64(cmd.ExecuteScalar()); }
    public void Restart() { Money.Dispose(); Money=new PokerMoneyLedger(ConnectionString); }
    public PokerFundedTable NewTable(PokerTableOptions? options=null,long reserve=0) =>
        new(Money,new PokerTableKey(Guid.NewGuid(),Guid.Empty,"table"),Currency,new PokerRules(6,100,5,10,30),options??new(),reserve);
    public void Dispose() { Money.Dispose(); try { File.Delete(_path);File.Delete(_path+".poker.lock"); } catch { } }
}
