using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

var passed=0;var failed=0;
void Test(string n,Action a){try{a();++passed;Console.WriteLine("PASS blackjack integration: "+n);}catch(Exception e){++failed;Console.Error.WriteLine("FAIL blackjack integration: "+n+"\n"+e);}}
void Check(bool v,string m){if(!v)throw new InvalidOperationException(m);}
void Throws(Action a){try{a();}catch{return;}throw new Exception("Expected rejection");}
Test("Poker zero-value event remains compatible; blackjack settings validate independently",()=>
{
    Check((int)MiniGameType.Poker==0 && (int)MiniGameType.Blackjack==1,"enum");
    var c=new StartMiniGameCommand();Check(c.HasValidSettings(),"old defaults");c.Game=MiniGameType.Blackjack;
    Check(c.HasValidSettings(),"blackjack defaults");c.BlackjackMinimumBet=3;Check(!c.HasValidSettings(),"odd wager");
    c.BlackjackMinimumBet=10;c.NpcPlayers=5;Check(!c.HasValidSettings(),"dealer/human seats not reserved");
    c.NpcPlayers=4;Check(c.HasValidSettings(),"five players plus dealer");
});
Test("Blackjack motion settings survive command, session projection and wire shape",()=>
{
    var motion=new PokerMotionSet(PokerMotionSpeed.Cinematic,false,true,false,true,false,true);
    var settings=new BlackjackSettings(new BlackjackRules(5,100,10,100,30),0,false,Guid.Empty,0,
        Guid.Empty,Guid.Empty,false,0,motion);
    var t=new BlackjackSessionTable(new(Guid.NewGuid(),Guid.Empty,"motion"),settings,null,new MemoryMiniGameProgressStore());
    var player=Guid.NewGuid();t.Join(player,"Alice",new MiniGameProgress(),null);
    var s=t.Project(player);
    Check(s.ProceduralAnimationSpeed==PokerMotionSpeed.Cinematic,"speed");
    Check(!s.AnimateDealCards && s.AnimateBoardCards && !s.AnimateChips && s.AnimateShowdown && !s.AnimateShuffle && s.AnimateAllIn,"flags");
    Check(s.HasValidShape(),"shape");
    var p=new BlackjackStatePacket{TableInstanceId=t.Id,ViewId=Guid.NewGuid(),PlayerId=player,Sequence=1,TableName="motion",State=s};
    Check(p.IsValid,"packet");
});
Test("Actual inventory debit and refund share blackjack settlement escrow",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var bank=f.Bank();f.Ledger.SettleBlackjack(f.Table,bank.Id,1,new Dictionary<Guid,long>{{p.Id,115},{bank.Id,985}});
    f.Ledger.Release(p.Id);var claim=f.Ledger.Refunds(f.Player).Single();Check(claim.Amount==115,"net blackjack amount");
    f.Ledger.Cashout(claim,f.Credit(115));f.Ledger.Cashout(claim,(_,_)=>throw new Exception("Duplicate refund"));
    Check(f.Inventory()==365,"inventory refund");Check(f.Ledger.BlackjackProfile(f.Player).Experience==25,"XP");
    Check(f.Ledger.Profile(f.Player).Experience==0,"poker XP contaminated");
});
Test("Duplicate blackjack settlement cannot duplicate XP or change a result",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var b=f.Bank();var close=new Dictionary<Guid,long>{{p.Id,110},{b.Id,990}};
    f.Ledger.SettleBlackjack(f.Table,b.Id,1,close);f.Ledger.SettleBlackjack(f.Table,b.Id,1,close);
    Check(f.Ledger.BlackjackProfile(f.Player).Wins==1,"double XP");close[p.Id]=120;close[b.Id]=980;
    Throws(()=>f.Ledger.SettleBlackjack(f.Table,b.Id,1,close));
});
Test("Blackjack rejects currency creation and mismatched bank receipts",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var b=f.Bank();
    Throws(()=>f.Ledger.SettleBlackjack(f.Table,b.Id,1,new Dictionary<Guid,long>{{p.Id,115},{b.Id,986}}));
    Check(f.Ledger.BlackjackProfile(f.Player).Experience==0,"failed settlement gave XP");
    Throws(()=>f.Ledger.SettleBlackjack(f.Table,p.Id,1,new Dictionary<Guid,long>{{p.Id,100},{b.Id,1000}}));
});
Test("Atomic failure rolls balances and blackjack XP back together",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var b=f.Bank();f.Ledger.Fault=point=>{if(point=="before-blackjack-settlement-commit")throw new IOException("fault");};
    Throws(()=>f.Ledger.SettleBlackjack(f.Table,b.Id,1,new Dictionary<Guid,long>{{p.Id,115},{b.Id,985}}));
    Check(f.Ledger.BlackjackProfile(f.Player).Experience==0,"partial XP");f.Restart();
    Check(f.Ledger.Refunds(f.Player).Single().Amount==100,"partial money");
});
Test("Restart voids unfinished rounds, preserves completed blackjack results",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var b=f.Bank();f.Ledger.SettleBlackjack(f.Table,b.Id,1,new Dictionary<Guid,long>{{p.Id,90},{b.Id,1010}});
    f.Restart();Check(f.Ledger.Refunds(f.Player).Single().Amount==90 && f.Ledger.HouseAvailable("blackjack:fixture")==10010,"restart");
    f.Ledger.Cashout(f.Ledger.Refunds(f.Player).Single(),f.Credit(90));f.Restart();Check(f.Inventory()==340 && f.Ledger.Refunds(f.Player).Length==0,"restart replay");
});
Test("No free reseed after dealer reserve is exhausted",()=>
{
    using var f=new MoneyFixture();var b=f.Ledger.OpenNpc(Guid.NewGuid(),f.Table,f.Currency,"blackjack:empty",100,100)!;
    Check(f.Ledger.OpenNpc(Guid.NewGuid(),Guid.NewGuid(),f.Currency,"blackjack:empty",10000,10)==null,"reseed");
    f.Ledger.Release(b.Id);Check(f.Ledger.HouseAvailable("blackjack:empty")==100,"release");
});
Test("A poker-funded seat blocks a simultaneous blackjack-funded seat",()=>
{
    using var f=new MoneyFixture();f.Human();Throws(()=>f.Ledger.OpenHuman(Guid.NewGuid(),Guid.NewGuid(),f.Player,f.Currency,"poker:other",100,f.Debit(100)));
    Check(f.Inventory()==250,"second debit");
});
Test("Separate blackjack unlocks and chosen back survive restart",()=>
{
    using var f=new MoneyFixture();var p=f.Human();var b=f.Bank();Throws(()=>f.Ledger.SelectBlackjackBack(f.Player,1));
    for(var hand=1;hand<=40;hand++)f.Ledger.SettleBlackjack(f.Table,b.Id,hand,new Dictionary<Guid,long>{{p.Id,100+hand},{b.Id,1000-hand}});
    f.Ledger.SelectBlackjackBack(f.Player,1);Check(f.Ledger.BlackjackProfile(f.Player).Level==5,"level curve");f.Restart();
    Check(f.Ledger.BlackjackProfile(f.Player).SelectedBack==1 && f.Ledger.Profile(f.Player).SelectedBack==0,"saved back separation");
});
Test("Full-inventory failure retains the claim",()=>
{
    using var f=new MoneyFixture();var p=f.Human();f.Ledger.Release(p.Id);var refund=f.Ledger.Refunds(f.Player).Single();
    Throws(()=>f.Ledger.Cashout(refund,(_,_)=>throw new MoneyRuleException("full")));Check(f.Ledger.Refunds(f.Player).Single().Amount==100 && f.Inventory()==250,"lost claim");
});
Test("Funded blackjack session completes independently with named NPC guests",()=>
{
    using var f=new MoneyFixture();var settings=new BlackjackSettings(new BlackjackRules(5,100,10,100,5),2,false,f.Currency,50000,Guid.Empty,Guid.Empty,true,0);
    var t=new BlackjackSessionTable(new(Guid.NewGuid(),Guid.Empty,"bj"),settings,f.Ledger,new MemoryMiniGameProgressStore());
    var p=f.Ledger.OpenHuman(Guid.NewGuid(),t.Id,f.Player,f.Currency,t.House,100,f.Debit(100));t.Join(f.Player,"Alice",t.Profile(f.Player),p);
    var now=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);Check(t.Start(f.Player,t.Core.Revision,now)==BlackjackError.None,"start");
    Check(t.Core.Snapshot(f.Player).Seats.Count(s=>s.Npc)==2,"NPC guests missing");
    Check(t.Bet(f.Player,t.Core.HandId,t.Core.Revision,10,now)==BlackjackError.None,"bet");
    for(var i=1;i<=100 && t.Core.Stage!=BlackjackStage.Finished;i++)t.Tick(now.AddSeconds(i));
    Check(t.Core.Stage==BlackjackStage.Finished,"round stuck");var s=t.Project(f.Player);Check(s.HasValidShape(),"invalid snapshot");
    var balance=s.Seats.Single(s=>s.PlayerId==f.Player).Chips;
    t.Leave(f.Player,now.AddMinutes(3));Check(t.Empty,"table not empty");var refund=f.Ledger.Refunds(f.Player).Single();Check(refund.Amount==balance,"session cashout");
    Check(f.Ledger.Profile(f.Player).Wins==0,"poker profile");
});
Test("Test blackjack progress is separate by game and never becomes funded XP",()=>
{
    using var f=new MoneyFixture();var store=new MemoryMiniGameProgressStore();store.AwardWin(f.Player,"blackjack",Guid.NewGuid(),1);
    Check(store.Load(f.Player,"poker").Experience==0 && f.Ledger.BlackjackProfile(f.Player).Experience==0,"test mixing");
});
var registry=new PacketTypeRegistry(NullLogger.Instance,typeof(IntersectPacket).Assembly);
Check(registry.TryRegisterBuiltIn(),"packet registry");PackedIntersectPacket.AddKnownTypes(registry.Types);
BlackjackStatePacket Packet(Guid player,long sequence=1)=>new(){TableInstanceId=Guid.NewGuid(),ViewId=Guid.NewGuid(),PlayerId=player,Sequence=sequence,TableName="blackjack-test",ServerUnixMs=100000,
    State=new(){HandId=1,Revision=1,Stage=BlackjackStage.Players,ActingSeat=0,ActingHand=0,DealerCards=[7],DealerTotal=9,DealerHoleHidden=true,
    MinimumBet=2,MaximumBet=100,Bank=1000,DeadlineUnixMs=105000,CanHit=true,CanStand=true,
    Seats=[new(){PlayerId=player,Seat=0,Name="Alice",Chips=90,InRound=true,Hands=[new(){Cards=[3,4],Total=11,Bet=10}]}]}};
BlackjackStatePacket Wire(BlackjackStatePacket p)=>(BlackjackStatePacket)(MessagePacker.Instance.Deserialize(p.Data)??throw new Exception("wire"));
Test("New packets are registered and round-trip the real engine envelope",()=>
{
    Check(registry.IsRegistered(typeof(BlackjackRequestPacket)) && registry.IsRegistered(typeof(BlackjackStatePacket)),"missing packets");
    var p=Packet(Guid.NewGuid());var copy=Wire(p);Check(copy.IsValid && copy.State!.DealerCards.Length==1 && copy.State.DealerTotal==9,"hole confidentiality");
});
Test("Malformed requests and face-down dealer totals are rejected",()=>
{
    var p=Packet(Guid.NewGuid());var request=new BlackjackRequestPacket{TableInstanceId=p.TableInstanceId,ViewId=p.ViewId,RequestId=1,Kind=BlackjackRequestKind.Bet,Amount=3};
    Check(!request.IsValid,"odd bet accepted");request.Amount=10;Check(request.IsValid,"valid bet");request.Kind=(BlackjackRequestKind)99;Check(!request.IsValid,"unknown command");
    p.State!.DealerTotal=20;Check(!p.IsValid,"derived hidden total accepted");
});
Test("Duplicate cards from different physical decks are valid",()=>
{
    var p=Packet(Guid.NewGuid());p.State!.Seats[0].Hands[0].Cards=[12,12];p.State.Seats[0].Hands[0].Total=12;p.State.Seats[0].Hands[0].Soft=true;
    Check(p.IsValid && Wire(p).IsValid,"multi deck duplicates");
});
Test("Anti-replay guard binds table, view and monotonic request sequence",()=>
{
    var p=Packet(Guid.NewGuid());var guard=new BlackjackRequestGuard(p.TableInstanceId,p.ViewId);var q=new BlackjackRequestPacket{TableInstanceId=p.TableInstanceId,ViewId=p.ViewId,RequestId=1,Kind=BlackjackRequestKind.Hit};
    Check(guard.Accept(q,1000) && !guard.Accept(q,1001),"replay");q.RequestId=2;q.ViewId=Guid.NewGuid();Check(!guard.Accept(q,1002),"wrong view");
});
Test("Leave remains accepted when actions are throttled",()=>
{
    var p=Packet(Guid.NewGuid());var guard=new BlackjackRequestGuard(p.TableInstanceId,p.ViewId);var q=new BlackjackRequestPacket{TableInstanceId=p.TableInstanceId,ViewId=p.ViewId,Kind=BlackjackRequestKind.Refresh};
    for(var n=1;n<=8;n++){q.RequestId=n;Check(guard.Accept(q,1000),"premature throttle");}q.RequestId=9;Check(!guard.Accept(q,1000),"burst");q.Kind=BlackjackRequestKind.Leave;Check(guard.Accept(q,1000),"trapped leave");
});
Test("No automatic retry of a wager or double on missing acknowledgement",()=>
{
    var player=Guid.NewGuid();var p=Packet(player);var m=new BlackjackClientModel();Check(m.Apply(p,player,1000),"apply");
    Check(m.Request(BlackjackRequestKind.Double,1000)!=null && m.Request(BlackjackRequestKind.Double,1001)==null,"double click");
    Check(m.NeedsRefresh(6000),"refresh missing");var refresh=m.Request(BlackjackRequestKind.Refresh,6000);Check(refresh?.Amount==0 && refresh.Kind==BlackjackRequestKind.Refresh,"resubmitted money");
});
Test("Late acknowledgements never roll a newer state back",()=>
{
    var player=Guid.NewGuid();var p=Packet(player);var m=new BlackjackClientModel();m.Apply(p,player,1000);var q=m.Request(BlackjackRequestKind.Hit,1000)!;
    var ack=Wire(p);ack.Sequence=2;ack.RequestId=q.RequestId;var newer=Wire(p);newer.Sequence=3;newer.State!.Revision=4;
    m.Apply(newer,player,1100);m.Apply(ack,player,1200);Check(!m.Pending && m.Current!.State!.Revision==4,"rollback");
});
Test("A dismissed window cannot reopen from stale packets",()=>
{
    var player=Guid.NewGuid();var p=Packet(player);var m=new BlackjackClientModel();m.Apply(p,player,1000);m.Dismiss();p.Sequence=2;Check(!m.Apply(p,player,1100),"reopened");
    p.ViewId=Guid.NewGuid();p.Sequence=3;Check(m.Apply(p,player,1200),"new view rejected");
});
Test("Dealer reveal and Refresh do not replay a deal animation",()=>
{
    var player=Guid.NewGuid();var p=Packet(player);var m=new BlackjackClientModel();m.Apply(p,player,1000);Check(!m.ObserveDeal(),"historical replay");
    p.Sequence=2;p.State!.DealerHoleHidden=false;p.State.Stage=BlackjackStage.Dealer;p.State.DealerCards=[7,6];p.State.DealerTotal=17;m.Apply(p,player,1100);
    Check(!m.ObserveDeal(),"hole reveal was a new drawn card");p.Sequence=3;m.Apply(p,player,1200);Check(!m.ObserveDeal(),"refresh replay");
});
Test("Recipient identity and clock are checked",()=>
{
    var player=Guid.NewGuid();var p=Packet(player);var m=new BlackjackClientModel();Check(!m.Apply(p,Guid.NewGuid(),1000),"other recipient");m.Apply(p,player,1000);Check(m.Seconds(2000)==4,"monotonic countdown");
});
Console.WriteLine($"{passed}/{passed+failed} blackjack integration groups passed.");Environment.ExitCode=failed==0?0:1;

internal sealed class MoneyFixture:IDisposable
{
    private readonly string _path=Path.Combine(Path.GetTempPath(),"blackjack-money-"+Guid.NewGuid()+".db");
    public Guid Player=Guid.NewGuid(),Currency=Guid.NewGuid(),Table=Guid.NewGuid();
    public PokerMoneyLedger Ledger {get;private set;}
    private string Connection=>new SqliteConnectionStringBuilder{DataSource=_path,Pooling=false}.ToString();
    public MoneyFixture(){using var c=new SqliteConnection(Connection);c.Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE TestInventory(Amount INTEGER NOT NULL CHECK(Amount>=0)); INSERT INTO TestInventory VALUES(350);";cmd.ExecuteNonQuery();Ledger=new(Connection);}
    public Action<SqliteConnection,SqliteTransaction> Debit(long n)=>(c,t)=>{using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText="UPDATE TestInventory SET Amount=Amount-$n WHERE Amount >= $n";cmd.Parameters.AddWithValue("$n",n);if(cmd.ExecuteNonQuery()!=1)throw new MoneyRuleException("insufficient inventory");};
    public Action<SqliteConnection,SqliteTransaction> Credit(long n)=>(c,t)=>{using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText="UPDATE TestInventory SET Amount=Amount+$n";cmd.Parameters.AddWithValue("$n",n);cmd.ExecuteNonQuery();};
    public MoneySeat Human()=>Ledger.OpenHuman(Guid.NewGuid(),Table,Player,Currency,"blackjack:fixture",100,Debit(100));
    public MoneySeat Bank()=>Ledger.OpenNpc(Guid.NewGuid(),Table,Currency,"blackjack:fixture",10000,1000)!;
    public long Inventory(){using var c=new SqliteConnection(Connection);c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Amount FROM TestInventory";return Convert.ToInt64(cmd.ExecuteScalar());}
    public void Restart(){Ledger.Dispose();Ledger=new(Connection);}
    public void Dispose(){Ledger.Dispose();try{File.Delete(_path);File.Delete(_path+".poker.lock");}catch{}}
}
