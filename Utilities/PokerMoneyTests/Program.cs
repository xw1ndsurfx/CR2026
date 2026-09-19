using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Microsoft.Data.Sqlite;

var passed=0; var failed=0;
void Test(string name, Action<Fixture> run)
{
    using var f=new Fixture();
    try { run(f); ++passed; Console.WriteLine("PASS funded poker: "+name); }
    catch(Exception e) { ++failed; Console.Error.WriteLine("FAIL funded poker: "+name+"\n"+e); }
}
Test("Buy-in uses selected inventory currency", f=>
{ var s=f.Buy(f.A,100); Check(f.Balance(f.A)==250 && f.Amount(s.Id)==100,"Debit/escrow"); Check(f.Balance(f.A,f.OtherCurrency)==777,"Other currency"); });
Test("Insufficient funds rolls back inventory and escrow", f=>
{ Throws(()=>f.Buy(f.A,400)); Check(f.Balance(f.A)==350 && f.Count("PokerMoneySeats")==0,"Partial debit"); });
Test("Buy-in receipt replay never calls inventory twice", f=>
{ var s=f.Buy(f.A,100); f.Money.OpenHuman(s.Id,s.Table,s.Character,s.Currency,s.House,100,(_,_)=>throw new Exception("replayed")); Check(f.Balance(f.A)==250 && f.Count("PokerMoneyTransfers")==1,"Duplicate debit"); });
Test("Pre-commit failure rolls back and permits retry", f=>
{
    f.Money.Fault=p=>{if(p=="before-commit")throw new IOException("fault");}; Throws(()=>f.Buy(f.A,100));
    Check(f.Balance(f.A)==350 && f.Count("PokerMoneySeats")==0,"Rollback"); f.Money.Fault=null; f.Buy(f.A,100); Check(f.Balance(f.A)==250,"Retry");
});
Test("Lost commit acknowledgement is resolved by receipt", f=>
{ f.Money.Fault=p=>{if(p=="after-commit")throw new IOException("ack");}; var s=f.Buy(f.A,100); Check(f.Balance(f.A)==250 && f.Amount(s.Id)==100,"Resolution"); });
Test("A character cannot buy a second active seat", f=>
{ f.Buy(f.A,100); Throws(()=>f.Buy(f.A,100)); Check(f.Balance(f.A)==250,"Second debit"); });
Test("Balances, XP and hand receipt commit together", f=>
{
    var a=f.Buy(f.A,100);var b=f.Buy(f.B,100); f.Money.Settle(f.TableId,1,new Dictionary<Guid,long>{{a.Id,125},{b.Id,75}});
    Check(f.Amount(a.Id)==125 && f.Amount(b.Id)==75,"Balances"); Check(f.Money.Profile(f.A).Experience==25 && f.Money.Profile(f.B).Experience==0,"XP");
    f.Pay(a);f.Pay(b); Check(f.Balance(f.A)==375 && f.Balance(f.B)==325,"Cash-out");
});
Test("Settlement replay cannot duplicate XP or change results", f=>
{
    var a=f.Buy(f.A,100);var b=f.Buy(f.B,100);var v=new Dictionary<Guid,long>{{a.Id,150},{b.Id,50}};
    f.Money.Settle(f.TableId,1,v);f.Money.Settle(f.TableId,1,v);Check(f.Money.Profile(f.A).Experience==25 && f.Money.Profile(f.A).Wins==1,"Duplicate XP");
    v[a.Id]=160;v[b.Id]=40;Throws(()=>f.Money.Settle(f.TableId,1,v));Check(f.Amount(a.Id)==150,"Changed result");
});
Test("Unbalanced settlements cannot mint currency", f=>
{
    var a=f.Buy(f.A,100);var b=f.Buy(f.B,100);Throws(()=>f.Money.Settle(f.TableId,1,new Dictionary<Guid,long>{{a.Id,150},{b.Id,100}}));
    Check(f.Amount(a.Id)==100 && f.Money.Profile(f.A).Experience==0,"Partial result");
});
Test("Failed hand commit keeps the whole previous checkpoint", f=>
{
    var a=f.Buy(f.A,100);var b=f.Buy(f.B,100);f.Money.Fault=p=>{if(p=="before-settlement-commit")throw new IOException("fault");};
    Throws(()=>f.Money.Settle(f.TableId,1,new Dictionary<Guid,long>{{a.Id,130},{b.Id,70}}));
    Check(f.Amount(a.Id)==100 && f.Amount(b.Id)==100 && f.Money.Profile(f.A).Experience==0,"Partial settlement");
});
Test("Unfinished hand is voided for all after restart", f=>
{
    f.Buy(f.A,100);f.Buy(f.B,100);f.Restart();Check(f.Money.Refunds(f.A).Single().Amount==100 && f.Money.Refunds(f.B).Single().Amount==100,"Recovery");
    f.RefundAll(f.A);f.RefundAll(f.B);Check(f.Balance(f.A)==350 && f.Balance(f.B)==350,"Lost buy-in");
});
Test("Committed result and XP survive restart", f=>
{
    var a=f.Buy(f.A,100);var b=f.Buy(f.B,100);f.Money.Settle(f.TableId,1,new Dictionary<Guid,long>{{a.Id,125},{b.Id,75}});
    f.Restart();f.RefundAll(f.A);f.RefundAll(f.B);Check(f.Balance(f.A)==375 && f.Balance(f.B)==325 && f.Money.Profile(f.A).Experience==25,"Lost result");
});
Test("Full inventory keeps an unpaid claim", f=>
{
    var a=f.Buy(f.A,100);f.Money.Release(a.Id);var claim=f.Money.Refunds(f.A).Single();
    Throws(()=>f.Money.Cashout(claim,(_,_)=>throw new MoneyRuleException("full")));
    Check(f.Money.Refunds(f.A).Single().Amount==100 && f.Balance(f.A)==250,"Lost claim");f.RefundAll(f.A);Check(f.Balance(f.A)==350,"Retry refund");
});
Test("Concurrent repeated cash-outs credit only once", f=>
{
    var a=f.Buy(f.A,100);f.Money.Release(a.Id);var s=f.Money.Refunds(f.A).Single();
    Parallel.For(0,12,_=>f.Money.Cashout(s,(c,t)=>f.Adjust(c,t,f.A,f.Currency,100)));
    Check(f.Balance(f.A)==350 && f.Money.Refunds(f.A).Length==0,"Duplicate refund");
});
Test("Cash-out acknowledgement loss does not duplicate credit", f=>
{var a=f.Buy(f.A,100);f.Money.Release(a.Id);f.Money.Fault=p=>{if(p=="after-commit")throw new IOException("ack");};f.RefundAll(f.A);f.Restart();f.RefundAll(f.A);Check(f.Balance(f.A)==350,"Duplicate credit");});
Test("NPC funds are finite and shared across instances", f=>
{
    var s=f.Money.OpenNpc(Guid.NewGuid(),f.TableId,f.Currency,"same-house",100,100)!;Check(f.Money.HouseAvailable("same-house")==0,"Loan not debited");
    Check(f.Money.OpenNpc(Guid.NewGuid(),Guid.NewGuid(),f.Currency,"same-house",999999,100)==null,"Reseed");f.Money.Release(s.Id);Check(f.Money.HouseAvailable("same-house")==100,"Loan not returned");
});
Test("Human wins deplete the house without free NPC refills", f=>
{
    var a=f.Buy(f.A,100);var s=f.Money.OpenNpc(Guid.NewGuid(),f.TableId,f.Currency,"finite",100,100)!;
    f.Money.Settle(f.TableId,1,new Dictionary<Guid,long>{{a.Id,200},{s.Id,0}});f.Money.Release(s.Id);f.Restart();
    Check(f.Money.OpenNpc(Guid.NewGuid(),Guid.NewGuid(),f.Currency,"finite",100000,100)==null,"Free refill");f.RefundAll(f.A);Check(f.Balance(f.A)==450,"Win cash-out");
});
Test("NPC loans return once on recovery", f=>
{f.Money.OpenNpc(Guid.NewGuid(),f.TableId,f.Currency,"house",1000,100);f.Restart();Check(f.Money.HouseAvailable("house")==1000,"Loan lost");f.Restart();Check(f.Money.HouseAvailable("house")==1000,"Double return");});
Test("A zero initial reserve does not consume the future seed", f=>
{Check(f.Money.OpenNpc(Guid.NewGuid(),f.TableId,f.Currency,"new-house",0,100)==null,"Unexpected NPC");Check(f.Money.OpenNpc(Guid.NewGuid(),f.TableId,f.Currency,"new-house",1000,100)!=null,"Seed consumed");});
Test("OS lease excludes a second recovery owner", f=>
{Throws(()=>{using var duplicate=new PokerMoneyLedger(f.ConnectionString);});});
Test("Locked cosmetics are rejected and B1 persists", f=>
{Throws(()=>f.Money.SelectBack(f.A,5));f.Money.SelectBack(f.A,0);f.Restart();Check(f.Money.Profile(f.A).SelectedBack==0 && f.Money.Profile(f.A).Experience==0,"Profile changed");});
Test("Two humans play the real engine with funded bankrolls", f=>
{
    var t=f.NewTable();t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");t.Join(f.Buy(f.B,100,t.Id,t.House),"Bob");var now=DateTimeOffset.UtcNow;
    Check(t.Start(f.A,t.Snapshot(f.A).Revision,now)==PokerError.None,"Start");var s=t.Snapshot(f.A);var actor=s.Seats.Single(p=>p.Seat==s.ActingSeat).PlayerId;
    Check(s.Seats.All(p=>p.RevealedCards.Length==0),"Private leak");t.Act(actor,s.HandId,s.Revision,PokerAction.Fold,0,now.AddMilliseconds(10));
    Check(t.Snapshot(f.A).Phase==PokerPhase.Finished && t.Snapshot(f.A).Seats.Sum(p=>p.Chips)==200,"Hand");
    Check(f.Money.Profile(f.A).Experience+f.Money.Profile(f.B).Experience==25,"XP");
    t.Leave(f.A,now);t.Leave(f.B,now);f.RefundAll(f.A);f.RefundAll(f.B);Check(t.IsEmpty && f.Balance(f.A)+f.Balance(f.B)==700,"Conservation");
});
Test("Mid-hand departure waits for settlement, including auto-check streets", f=>
{
    var t=f.NewTable();t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");t.Join(f.Buy(f.B,100,t.Id,t.House),"Bob");var now=DateTimeOffset.UtcNow;
    t.Start(f.A,t.Snapshot(f.A).Revision,now);var s=t.Snapshot(f.A);var actor=s.Seats.Single(p=>p.Seat==s.ActingSeat).PlayerId;
    var waiting=actor==f.A?f.B:f.A;t.Leave(waiting,now);Check(f.Money.Refunds(waiting).Length==0,"Early payout");
    // Departing seats may check at no cost; drive the remaining player's real turns to showdown.
    for(var i=0;i<12 && t.Snapshot(actor).Phase!=PokerPhase.Finished;++i)
    {s=t.Snapshot(actor);now=now.AddMilliseconds(100);Check(s.ActingSeat==s.Seats.Single(p=>p.PlayerId==actor).Seat,"Unexpected actor");t.Act(actor,s.HandId,s.Revision,s.ToCall>0?PokerAction.Call:PokerAction.Check,0,now);}
    Check(t.Snapshot(actor).Phase==PokerPhase.Finished,"Hand unfinished");t.Tick(now);t.Leave(actor,now);f.RefundAll(f.A);f.RefundAll(f.B);
    Check(f.Balance(f.A)+f.Balance(f.B)==700,"Lost currency");
});
Test("Late nonparticipating seat leaves without changing the pot", f=>
{
    var t=f.NewTable();t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");t.Join(f.Buy(f.B,100,t.Id,t.House),"Bob");var now=DateTimeOffset.UtcNow;t.Start(f.A,t.Snapshot(f.A).Revision,now);
    var id=f.Account(350);t.Join(f.Buy(id,100,t.Id,t.House),"Charlie");Check(t.Snapshot(id).MyCards.Length==0,"Late cards");t.Leave(id,now);f.RefundAll(id);Check(f.Balance(id)==350,"Late refund");
    var s=t.Snapshot(f.A);var actor=s.Seats.Single(p=>p.Seat==s.ActingSeat).PlayerId;t.Act(actor,s.HandId,s.Revision,PokerAction.Fold,0,now.AddMilliseconds(1));Check(t.Snapshot(f.A).Phase==PokerPhase.Finished,"Settlement");
});
Test("Failed settlement freezes then retries without duplicate XP", f=>
{
    var t=f.NewTable();t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");t.Join(f.Buy(f.B,100,t.Id,t.House),"Bob");var now=DateTimeOffset.UtcNow;t.Start(f.A,t.Snapshot(f.A).Revision,now);
    var s=t.Snapshot(f.A);var actor=s.Seats.Single(p=>p.Seat==s.ActingSeat).PlayerId;f.Money.Fault=p=>{if(p=="before-settlement-commit")throw new IOException("fault");};
    Throws(()=>t.Act(actor,s.HandId,s.Revision,PokerAction.Fold,0,now.AddMilliseconds(1)));t.Suspend(now);Check(t.Pending && t.Presentation(f.A).NetWin==0,"Uncommitted win");
    f.Money.Fault=null;t.Tick(now.AddSeconds(3));t.Tick(now.AddSeconds(4));Check(!t.Pending && f.Money.Profile(f.A).Experience+f.Money.Profile(f.B).Experience==25,"Retry XP");
});
Test("Named NPCs play with debited house bankrolls", f=>
{
    var t=f.NewTable(new PokerTableOptions(true,2,false),3000);t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");var now=DateTimeOffset.UtcNow;t.Tick(now);var s=t.Snapshot(f.A);
    Check(s.Seats.Length==4 && s.Seats.Any(p=>p.Name=="Marlow"),"NPC seats");Check(f.Money.HouseAvailable(t.House)==2700,"NPC loans");t.Start(f.A,s.Revision,now);
    for(var i=0;i<300 && t.Snapshot(f.A).Phase!=PokerPhase.Finished;++i)
    {now=now.AddMilliseconds(500);s=t.Snapshot(f.A);if(s.Seats.Single(p=>p.Seat==s.ActingSeat).PlayerId==f.A)t.Act(f.A,s.HandId,s.Revision,s.ToCall>0?PokerAction.Call:PokerAction.Check,0,now);else t.Tick(now);}
    Check(t.Snapshot(f.A).Phase==PokerPhase.Finished,"NPC game stalled");t.Leave(f.A,now);f.RefundAll(f.A);Check(t.IsEmpty && f.Balance(f.A)+f.Money.HouseAvailable(t.House)==3350,"NPC/human conservation");
});
Test("Receipt replay rejects another currency, character or table", f=>
{
    var a=f.Buy(f.A,100);
    Throws(()=>f.Money.OpenHuman(a.Id,a.Table,f.B,a.Currency,a.House,100,(_,_)=>{}));
    Throws(()=>f.Money.OpenHuman(a.Id,a.Table,f.A,f.OtherCurrency,a.House,100,(_,_)=>{}));
    Throws(()=>f.Money.OpenHuman(a.Id,Guid.NewGuid(),f.A,a.Currency,a.House,100,(_,_)=>{}));
    Check(f.Balance(f.A)==250,"Replay changed inventory");
});
Test("An orphan admission can be released and refunded once", f=>
{f.Buy(f.A,100);f.Money.ReleaseOrphanHuman(f.A);f.Money.ReleaseOrphanHuman(f.A);f.RefundAll(f.A);Check(f.Balance(f.A)==350,"Orphan refund");});
Test("Depleted dealer still reserves its chair", f=>
{
    var t=f.NewTable(new PokerTableOptions(true,0,false),0);t.Join(f.Buy(f.A,100,t.Id,t.House),"Alice");
    for(var i=0;i<4;++i){Check(t.CanJoin(),"Premature capacity");var id=f.Account(350);t.Join(f.Buy(id,100,t.Id,t.House),"Guest"+i);}
    Check(!t.CanJoin(),"Dealer chair stolen");t.Tick(DateTimeOffset.UtcNow);Check(t.Snapshot(f.A).Seats.Length==5,"Depleted NPC synthesized");
});
Console.WriteLine($"{passed}/{passed+failed} funded poker groups passed.");Environment.ExitCode=failed==0?0:1;
static void Check(bool v,string m){if(!v)throw new InvalidOperationException(m);}
static void Throws(Action action){try{action();}catch{return;}throw new InvalidOperationException("Expected rejection");}

internal sealed class Fixture:IDisposable
{
    private readonly string _path=Path.Combine(Path.GetTempPath(),"poker-money-"+Guid.NewGuid()+".db");
    public string ConnectionString{get;} public PokerMoneyLedger Money{get;private set;}
    public Guid Currency{get;}=Guid.NewGuid();public Guid OtherCurrency{get;}=Guid.NewGuid();
    public Guid TableId{get;}=Guid.NewGuid();public Guid A{get;}=Guid.NewGuid();public Guid B{get;}=Guid.NewGuid();
    public Fixture()
    {
        ConnectionString=new SqliteConnectionStringBuilder{DataSource=_path,Pooling=false}.ToString();
        using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE Inventory(Character TEXT,Currency TEXT,Amount INTEGER CHECK(Amount>=0),PRIMARY KEY(Character,Currency));";cmd.ExecuteNonQuery();}
        Seed(A,Currency,350);Seed(B,Currency,350);Seed(A,OtherCurrency,777);Money=new PokerMoneyLedger(ConnectionString);
    }
    private SqliteConnection Open(){var c=new SqliteConnection(ConnectionString);c.Open();return c;}
    public Guid Account(long n){var id=Guid.NewGuid();Seed(id,Currency,n);return id;}
    private void Seed(Guid person,Guid currency,long n)
    {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO Inventory VALUES($a,$c,$v)";cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",currency.ToString("N"));cmd.Parameters.AddWithValue("$v",n);cmd.ExecuteNonQuery();}
    public void Adjust(SqliteConnection c,SqliteTransaction t,Guid person,Guid currency,long delta)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText="UPDATE Inventory SET Amount=Amount+$v WHERE Character=$a AND Currency=$c AND Amount+$v>=0;";
        cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",currency.ToString("N"));cmd.Parameters.AddWithValue("$v",delta);if(cmd.ExecuteNonQuery()!=1)throw new MoneyRuleException("Insufficient inventory");
    }
    public MoneySeat Buy(Guid person,long n,Guid table=default,string house="test-house")=>Money.OpenHuman(Guid.NewGuid(),table==Guid.Empty?TableId:table,person,Currency,house,n,(c,t)=>Adjust(c,t,person,Currency,-n));
    public void Pay(MoneySeat s){Money.Release(s.Id);RefundAll(s.Character);}
    public void RefundAll(Guid person){foreach(var s in Money.Refunds(person))Money.Cashout(s,(c,t)=>Adjust(c,t,person,s.Currency,s.Amount));}
    public long Balance(Guid person,Guid currency=default)
    {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Amount FROM Inventory WHERE Character=$a AND Currency=$c";cmd.Parameters.AddWithValue("$a",person.ToString("N"));cmd.Parameters.AddWithValue("$c",(currency==Guid.Empty?Currency:currency).ToString("N"));return Convert.ToInt64(cmd.ExecuteScalar());}
    public long Amount(Guid seat){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Amount FROM PokerMoneySeats WHERE Id=$id";cmd.Parameters.AddWithValue("$id",seat.ToString("N"));return Convert.ToInt64(cmd.ExecuteScalar());}
    public long Count(string table){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM "+table;return Convert.ToInt64(cmd.ExecuteScalar());}
    public void Restart(){Money.Dispose();Money=new PokerMoneyLedger(ConnectionString);}
    public PokerFundedTable NewTable(PokerTableOptions? options=null,long reserve=0)=>new(Money,new PokerTableKey(Guid.NewGuid(),Guid.Empty,"table"),Currency,new PokerRules(6,100,5,10,30),options??new(),reserve);
    public void Dispose(){Money.Dispose();try{File.Delete(_path);File.Delete(_path+".poker.lock");}catch{}}
}
