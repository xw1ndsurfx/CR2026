using Intersect.Config;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.MiniGames.Currency;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// Real PlayerContext and real InventorySlot mappings. Only authentication/game assets are absent.
// Foreign keys are disabled only in this temporary fixture to seed inventory rows without
// inventing an unrelated account, map, class and character creation workflow.
var passed=0;var failed=0;
void Test(string name,Action<Fixture> test)
{
    try { using var f=new Fixture();test(f);++passed;Console.WriteLine("PASS real inventory: "+name); }
    catch(Exception e){++failed;Console.Error.WriteLine("FAIL real inventory: "+name+"\n"+e);}
}
Test("Actual inventory debit is persisted even with automatic detection disabled",f=>
{
    f.Buy(250);Check(f.Read(f.Slot).Quantity==250,"Inventory was not debited");
    Check(f.Scalar("SELECT Amount FROM PokerMoneySeats")==100,"Escrow missing");
    Check(f.Read(f.OtherSlot).Quantity==777,"Other inventory item changed");
});
Test("Inventory and escrow both roll back after a callback failure",f=>
{
    Throws(()=>f.Money.OpenHuman(Guid.NewGuid(),f.Table,f.Character,f.Currency,"house",100,(c,t)=>
    {f.Write(c,t,f.Character,f.Slot,new Item(f.Currency,250));throw new IOException("injected after EF SaveChanges");}));
    Check(f.Read(f.Slot).Quantity==350 && f.Scalar("SELECT COUNT(*) FROM PokerMoneySeats")==0,"Split transaction");
});
Test("Inventory rows are verified against the authenticated character",f=>
{
    Throws(()=>f.Money.OpenHuman(Guid.NewGuid(),f.Table,Guid.NewGuid(),f.Currency,"house",100,(c,t)=>
        f.Write(c,t,Guid.NewGuid(),f.Slot,new Item(f.Currency,250))));
    Check(f.Read(f.Slot).Quantity==350 && f.Scalar("SELECT COUNT(*) FROM PokerMoneySeats")==0,"Foreign row modified");
});
Test("Actual row cash-out commits with receipt and survives reopening",f=>
{
    var s=f.Buy(250);f.Money.Release(s.Id);var claim=f.Money.Refunds(f.Character).Single();
    f.Money.Cashout(claim,(c,t)=>f.Write(c,t,f.Character,f.Slot,new Item(f.Currency,350)));
    f.Money.Cashout(claim,(_,_)=>throw new Exception("Duplicate inventory credit"));
    Check(f.Read(f.Slot).Quantity==350 && f.Scalar("SELECT COUNT(*) FROM PokerMoneyTransfers")==2,"Refund receipt");
    f.Restart();Check(f.Money.Refunds(f.Character).Length==0 && f.Read(f.Slot).Quantity==350,"Replay after restart");
});
Test("Empty-slot item identity and quantity are stored together",f=>
{
    var s=f.Buy(0,350);Check(f.Read(f.Slot).ItemId==Guid.Empty && f.Read(f.Slot).Quantity==0,"Empty slot not persisted");
    f.Money.Release(s.Id);var claim=f.Money.Refunds(f.Character).Single();
    f.Money.Cashout(claim,(c,t)=>f.Write(c,t,f.Character,f.EmptySlot,new Item(f.Currency,350)));
    Check(f.Read(f.EmptySlot).ItemId==f.Currency && f.Read(f.EmptySlot).Quantity==350,"New stack not persisted");
});
Test("A rolled-back cash-out leaves the claim available",f=>
{
    var s=f.Buy(250);f.Money.Release(s.Id);var claim=f.Money.Refunds(f.Character).Single();
    Throws(()=>f.Money.Cashout(claim,(c,t)=>{f.Write(c,t,f.Character,f.Slot,new Item(f.Currency,350));throw new IOException("failed refund");}));
    Check(f.Read(f.Slot).Quantity==250 && f.Money.Refunds(f.Character).Single().Amount==100,"Lost refund");
});
Console.WriteLine($"{passed}/{passed+failed} real inventory groups passed.");Environment.ExitCode=failed==0?0:1;
static void Check(bool v,string m){if(!v)throw new InvalidOperationException(m);}
static void Throws(Action a){try{a();}catch{return;}throw new InvalidOperationException("Expected rejection");}

internal sealed class Fixture:IDisposable
{
    private readonly string _path=Path.Combine(Path.GetTempPath(),"poker-real-inventory-"+Guid.NewGuid()+".db");
    private readonly SqliteConnectionStringBuilder _builder;
    public PokerMoneyLedger Money{get;private set;}
    public Guid Character{get;}=Guid.NewGuid();public Guid Currency{get;}=Guid.NewGuid();public Guid Table{get;}=Guid.NewGuid();
    public Guid Slot{get;}=Guid.NewGuid();public Guid OtherSlot{get;}=Guid.NewGuid();public Guid EmptySlot{get;}=Guid.NewGuid();
    public Fixture()
    {
        _builder=new SqliteConnectionStringBuilder{DataSource=_path,Pooling=false,ForeignKeys=false};
        using(var c=Context())
        {
            c.Database.EnsureCreated();
            Seed(c,Slot,Currency,350,0);Seed(c,OtherSlot,Guid.NewGuid(),777,1);Seed(c,EmptySlot,Guid.Empty,0,2);
            c.ChangeTracker.DetectChanges();c.SaveChanges();
        }
        Money=new PokerMoneyLedger(_builder.ToString());
    }
    private PlayerContext Context()=>PlayerContext.Create(new DatabaseContextOptions
    {
        DatabaseType=DatabaseType.SQLite,ConnectionStringBuilder=_builder,ReadOnly=false,
        AutoDetectChanges=false,QueryTrackingBehavior=QueryTrackingBehavior.TrackAll,DisableAutoInclude=true,
        LoggerFactory=NullLoggerFactory.Instance,
    });
    private void Seed(PlayerContext c,Guid id,Guid currency,int n,int index)
    {
        var s=new InventorySlot(index);s.Set(new Item(currency,n));
        typeof(InventorySlot).GetProperty(nameof(InventorySlot.Id))!.SetValue(s,id);
        typeof(InventorySlot).GetProperty(nameof(InventorySlot.PlayerId))!.SetValue(s,Character);
        c.Player_Items.Add(s);
    }
    public InventorySlot Read(Guid id)
    {using var c=Context();return c.Player_Items.IgnoreAutoIncludes().AsNoTracking().Single(s=>s.Id==id);}
    public MoneySeat Buy(int remaining,long amount=100)=>Money.OpenHuman(Guid.NewGuid(),Table,Character,Currency,"house",amount,
        (c,t)=>Write(c,t,Character,Slot,remaining==0?Item.None:new Item(Currency,remaining)));
    public void Write(SqliteConnection connection,SqliteTransaction transaction,Guid player,Guid id,Item value)
    {
        using var c=Context();c.ChangeTracker.AutoDetectChangesEnabled=false;
        PokerInventoryWriter.Write(c,connection,transaction,player,new Dictionary<Guid,Item>{{id,value}});
    }
    public long Scalar(string sql){using var c=new SqliteConnection(_builder.ToString());c.Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToInt64(cmd.ExecuteScalar());}
    public void Restart(){Money.Dispose();Money=new PokerMoneyLedger(_builder.ToString());}
    public void Dispose(){Money.Dispose();try{File.Delete(_path);File.Delete(_path+".poker.lock");}catch{}}
}
