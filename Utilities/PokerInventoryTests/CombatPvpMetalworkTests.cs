using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Server.Database;
using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.Pvp;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Entities;
using Intersect.Network.Packets.Pvp;
using Intersect.Network.Packets.Metalworking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
internal static class CombatPvpMetalworkTests
{
    [ModuleInitializer] internal static void Run(){try{Verify();}catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}}
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static void Verify()
    {
        var map=Guid.NewGuid();var boss=Guid.NewGuid();
        var config=new WorldActivitiesConfiguration {CombatMusic="normal.ogg",CombatMusicRules=[new(){MapIds=[map],Tracks=["a.ogg","b.ogg"]},new(){MapIds=[boss],Tracks=["boss.ogg"]}]};
        Check(config.IsValid && config.CombatTracksFor(map).Length==2 && config.CombatTracksFor(boss).Single()=="boss.ogg" && config.CombatTracksFor(Guid.NewGuid()).Single()=="normal.ogg","Map playlists and global fallback");
        config.CombatMusicRules[1].MapIds=[map];Check(!config.IsValid,"Ambiguous map playlists rejected");
        var ranks=new PvpConfiguration();Check(ranks.IsValid && ranks.Grades.Count==10 && ranks.Grade(99)=="Recruit" && ranks.Grade(100)=="Soldier" && ranks.Grade(5000)=="Grand Marshal","All ten rank boundaries");
        ranks.Grades[1].Points=0;Check(!ranks.IsValid,"Rank thresholds must increase");
        var arena=new CaptureChestArena {RedX=0,RedY=0,BlueX=10,BlueY=10,ScoreToWin=2};var a=Guid.NewGuid();var b=Guid.NewGuid();
        var round=new CaptureChestRound(arena,0);
        round.Tick(100,[new(a,0,10,10,true),new(b,1,0,0,true)]);
        Check(round.Chests[1].Carrier==a && round.Chests[0].Carrier==b,"Both teams can steal a chest");
        round.Tick(200,[new(a,0,0,0,true),new(b,1,5,5,true)]);Check(round.Scores[0]==0,"Cannot capture while own chest is stolen");
        round.Drop(b,300);Check(!round.Chests[0].Home && round.Chests[0].Carrier==Guid.Empty,"Death drops a carried chest");
        round.Tick(400,[new(a,0,5,5,true),new(b,1,10,10,false)]);Check(round.Chests[0].Home,"Touching own dropped chest returns it");
        round.Tick(500,[new(a,0,0,0,true)]);Check(round.Scores[0]==1 && round.Chests[1].Home,"Returning enemy chest scores and resets it");
        round.Tick(600,[new(a,0,10,10,true)]);round.Tick(700,[new(a,0,0,0,true)]);Check(round.Finished && round.Winner==0,"Score target ends the match");
        round.Tick(800,[new(a,0,10,10,true)]);Check(round.Scores[0]==2,"Completed match cannot score again");
        var expiry=new CaptureChestRound(arena,0);expiry.Tick(600000,[]);Check(expiry.Finished && expiry.Winner==-1,"Ten minute default ends in draw at exact boundary");
        var dropped=new CaptureChestRound(arena,0);dropped.Tick(1,[new(a,0,10,10,true)]);dropped.Tick(2,[new(a,0,5,5,true)]);dropped.Drop(a,3);dropped.Tick(30003,[]);Check(dropped.Chests[1].Home,"Abandoned chest returns after thirty seconds");
        var forfeit=new CaptureChestRound(arena,0);forfeit.Forfeit(1);Check(forfeit.Winner==0,"Forfeit awards opposing team");
        foreach(var kind in Enum.GetValues<MetalworkKind>())
        {
            var work=new MetalworkRound(kind,0);long last=-1000;
            for(long now=0;now<40000 && !work.Complete;now+=20)
            {
                work.Tick(now);
                if(work.Stage==MetalworkStage.Heating && work.Heat<62 && now-last>=360){if(work.Act(MetalworkAction.Pump,now))last=now;}
                else if(work.Stage!=MetalworkStage.Heating && now-last>=360 && Math.Abs(MetalworkRound.Cursor(work.Stage,now-work.StageStarted)-work.Target)<2){if(work.Act(MetalworkAction.Strike,now))last=now;}
            }
            Check(work.Complete && !work.TimedOut && work.Quality>=90,"Skilled play must earn high quality for "+kind);
            Check(!work.Act(MetalworkAction.Strike,50000),"Completed round ignores replayed input");
            var idle=new MetalworkRound(kind,0);idle.Tick(12000);idle.Tick(22000);Check(idle.TimedOut && idle.Complete,"Unattended round fails");
            var spam=new MetalworkRound(kind,0);Check(spam.Act(MetalworkAction.Pump,0) && !spam.Act(MetalworkAction.Pump,1),"Input rate enforced by server clock");
        }
        var npc=new NPCDescriptor(Guid.NewGuid()){Experience=10,ExperienceMaximum=20};var samples=Enumerable.Range(0,2000).Select(_=>npc.RollExperience()).ToArray();Check(samples.Min()==10 && samples.Max()==20 && samples.Distinct().Count()==11,"NPC XP range includes both ends");
        npc.ExperienceMaximum=0;Check(npc.RollExperience()==10,"Legacy NPCs retain fixed XP");npc.Experience=long.MaxValue-1;npc.ExperienceMaximum=long.MaxValue;Check(npc.RollExperience()>=long.MaxValue-1,"XP upper bound does not overflow");Check(npc.IndividualizedLoot,"New NPC loot defaults to every attacker");
        using(var connection=new SqliteConnection("Data Source=:memory:"))
        {
            connection.Open();using(var command=connection.CreateCommand()){command.CommandText="CREATE TABLE Npcs (Id TEXT, Experience INTEGER);INSERT INTO Npcs VALUES ('old',125)";command.ExecuteNonQuery();}
            using var context=new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);var migration=new Intersect.Server.Migrations.Sqlite.Game.NpcExperienceRange();var builder=new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");typeof(Intersect.Server.Migrations.Sqlite.Game.NpcExperienceRange).GetMethod("Up",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(migration,[builder]);
            foreach(var sql in context.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations)){using var command=connection.CreateCommand();command.CommandText=sql.CommandText;command.ExecuteNonQuery();}
            using(var command=connection.CreateCommand()){command.CommandText="SELECT Experience,ExperienceMaximum FROM Npcs";using var reader=command.ExecuteReader();Check(reader.Read() && reader.GetInt64(0)==125 && reader.GetInt64(1)==0,"Migration preserves existing NPC rewards");}
        }
        var packet=new PvpStatePacket {Grade="Captain",Points=1500,Match=new(){Team=1,RespawnSeconds=5,ScoreToWin=3,Chests=[new(){Carrier=a,X=7,Y=8}]}};var wire=MessagePack.MessagePackSerializer.Deserialize<PvpStatePacket>(MessagePack.MessagePackSerializer.Serialize(packet));Check(wire.Match!.Chests[0].Carrier==a && wire.Match.RespawnSeconds==5 && wire.Grade=="Captain","PvP state wire round trip");
        var metal=new MetalworkStatePacket {SessionId=a,Sequence=25,Stage=MetalworkStage.Quenching,Quality=90,Paid=true};var metalWire=MessagePack.MessagePackSerializer.Deserialize<MetalworkStatePacket>(MessagePack.MessagePackSerializer.Serialize(metal));Check(metalWire.Sequence==25 && metalWire.Paid,"Craft payment and ordering survive wire");
        var old=Options.Instance;typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options());
        var runtime=typeof(Player).Assembly.GetType("Intersect.Server.Pvp.PvpRuntime",true)!;var cache=typeof(Player).Assembly.GetType("Intersect.Server.WorldEvents.WorldActivitiesRuntime",true)!.GetField("_current",BindingFlags.Static|BindingFlags.NonPublic)!;var previous=cache.GetValue(null);
        var ore=new ItemDescriptor(Guid.NewGuid()){Name="Test ore",Stackable=true,MaxInventoryStack=100};var ingot=new ItemDescriptor(Guid.NewGuid()){Name="Test ingot",Stackable=true,MaxInventoryStack=100};ItemDescriptor.Lookup[ore.Id]=ore;ItemDescriptor.Lookup[ingot.Id]=ingot;
        try
        {
            cache.SetValue(null,new WorldActivitiesConfiguration {Pvp=new(){Enabled=true}});var p=new Player {Level=10};var other=new Player {Level=10};GC.SuppressFinalize(p);GC.SuppressFinalize(other);
            var flags=BindingFlags.Static|BindingFlags.NonPublic;runtime.GetMethod("Adjust",flags)!.Invoke(null,[p,150L,true]);Check((long)runtime.GetMethod("Points",flags)!.Invoke(null,[p])! ==150 && (string)runtime.GetMethod("Grade",flags)!.Invoke(null,[p])! =="Soldier","Player variables retain points and resolve grade");
            runtime.GetMethod("Adjust",flags)!.Invoke(null,[p,-1000L,false]);Check((long)runtime.GetMethod("Points",flags)!.Invoke(null,[p])! ==0,"Rank penalties cannot go below zero");
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var eligible=runtime.GetMethod("EligibleOpponent",flags)!;Check((bool)eligible.Invoke(null,[p,other,now])!,"Distinct similar-level opponents qualify");var key=(Guid)runtime.GetMethod("Key",flags)!.Invoke(null,["opponent/"+other.Id])!;p.SetVariableValue(key,now);Check(!(bool)eligible.Invoke(null,[p,other,now+1000])! && (bool)eligible.Invoke(null,[p,other,now+600000])!,"Repeated opponent cooldown persists in player variables");
            other.Level=100;Check(!(bool)eligible.Invoke(null,[p,other,now+700000])!,"Large level mismatch excluded");
            Array.Fill(p.Equipment,-1);p.Items.FillToCapacity();p.Items[0].Set(new Item(ore.Id,5));var craft=typeof(Player).Assembly.GetType("Intersect.Server.MiniGames.Metalworking.MetalworkRuntime",true)!.GetMethod("TryCraft",flags)!;var recipe=new MetalworkRecipe {Ingredients=[new(){ItemId=ore.Id,Quantity=3}],OutputItemId=ingot.Id};
            Check(!(bool)craft.Invoke(null,[p,recipe])! && p.Items[0].Quantity==5 && p.Items.All(i=>i.ItemId!=ingot.Id),"Missing account checkpoint cannot consume materials or issue outputs");
            var commit=craft.DeclaringType!.GetMethod("CommitCraft",flags)!;
            foreach(var result in Enum.GetValues<Intersect.Server.Database.PlayerData.UserSaveResult>().Where(v=>v!=Intersect.Server.Database.PlayerData.UserSaveResult.Completed))
            {
                Check(!(bool)commit.Invoke(null,[p,recipe,new Func<Intersect.Server.Database.PlayerData.UserSaveResult>(()=>result)])! && p.Items[0].Quantity==5 && p.Items.All(i=>i.ItemId!=ingot.Id),"Any rejected durable checkpoint must restore ingredients and output");
            }
            Check(!(bool)commit.Invoke(null,[p,recipe,new Func<Intersect.Server.Database.PlayerData.UserSaveResult>(()=>throw new IOException("injected save fault"))])! && p.Items[0].Quantity==5,"Throwing account checkpoint restores materials");
            var checkpoints=0;
            var committed=(bool)commit.Invoke(null,[p,recipe,new Func<Intersect.Server.Database.PlayerData.UserSaveResult>(()=>{checkpoints++;return Intersect.Server.Database.PlayerData.UserSaveResult.Completed;})])!;
            Check(committed && checkpoints==1 && p.Items[0].Quantity==2 && p.Items.Where(i=>i.ItemId==ingot.Id).Sum(i=>i.Quantity)==1,$"One committed craft consumes exactly its inputs and issues one output: committed={committed}, checkpoints={checkpoints}, ore={p.Items[0].Quantity}, output={p.Items.Where(i=>i.ItemId==ingot.Id).Sum(i=>i.Quantity)}, slots={p.Items.Count}");
            Check(!(bool)commit.Invoke(null,[p,recipe,new Func<Intersect.Server.Database.PlayerData.UserSaveResult>(()=>throw new Exception("must not save"))])! && p.Items[0].Quantity==2,"Insufficient ingredients reject before checkpoint");

        }
        finally{cache.SetValue(null,previous);ItemDescriptor.Lookup.DeleteAt(ore.Id);ItemDescriptor.Lookup.DeleteAt(ingot.Id);typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,old);}
        System.Console.WriteLine("PASS combat playlists, ten PvP grades, chest theft/return/score/death/timeout, server-timed foundry/forge, rank persistence/anti-farming, craft checkpoint, NPC XP migration and network packets");
    }
}
