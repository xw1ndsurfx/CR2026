using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.WorldEvents;
using Intersect.GameObjects;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
internal static class ExperienceLootBoosterTests
{
    [ModuleInitializer] internal static void Run(){try{Verify();}catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}}
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static void Verify()
    {
        var oldOptions=Options.Instance;typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options());
        var assembly=typeof(Player).Assembly;var flags=BindingFlags.Static|BindingFlags.NonPublic;var runtime=assembly.GetType("Intersect.Server.WorldEvents.WorldActivitiesRuntime",true)!;var cache=runtime.GetField("_current",flags)!;var previous=cache.GetValue(null);
        var potion=new ItemDescriptor(Guid.NewGuid()){Name="Learning draught",ItemType=ItemType.Consumable,Stackable=true,MaxInventoryStack=100};potion.Consumable.Type=ConsumableType.ExperienceBoost;potion.Consumable.Value=1800;potion.Consumable.Percentage=50;ItemDescriptor.Lookup[potion.Id]=potion;
        var loot=new ItemDescriptor(Guid.NewGuid()){Name="Test harvest",Stackable=true,MaxInventoryStack=100};ItemDescriptor.Lookup[loot.Id]=loot;
        try
        {
            var today=DateTimeOffset.Now;var evt=new ExperienceEventDefinition {Enabled=true,Days=127,StartHour=0,DurationMinutes=1440,BonusPercent=50};var config=new WorldActivitiesConfiguration {ExperienceEvents=[evt],LootEvents=[new(){Name="Loot celebration",Enabled=true,Days=127,StartHour=0,DurationMinutes=1440,BonusPercent=100}]};cache.SetValue(null,config);
            var snap=(LootEventAnnouncementPacket)runtime.GetMethod("LootSnapshot",flags)!.Invoke(null,[today,true])!;Check(snap.BonusPercent==100 && snap.EndsAtUnixMs>snap.ServerTimeUnixMs && snap.Silent,"Loot snapshot includes live schedule and silent login");
            Check(WorldActivitiesConfiguration.LootChance(20,50)==30 && WorldActivitiesConfiguration.LootChance(75,100)==100 && WorldActivitiesConfiguration.LootChance(0,1000)==0,"Loot bonus multiplies probability and caps at 100%");
            var friday=new DateTimeOffset(2026,10,16,23,30,0,TimeSpan.Zero);var night=new ExperienceEventDefinition {Enabled=true,Days=1<<(int)DayOfWeek.Friday,StartHour=23,DurationMinutes=120,BonusPercent=50};config.LootEvents=[night,new(){Enabled=true,Days=127,StartHour=0,DurationMinutes=1440,BonusPercent=25}];Check(config.LootBonusAt(friday.AddMinutes(60))==50 && config.LootBonusAt(friday.AddMinutes(90))==25,"Overnight loot events expire precisely and fall back to strongest remaining event");
            config.LootEvents=[new(){Enabled=true,Days=127,StartHour=0,DurationMinutes=1440,BonusPercent=100}];
            var player=new Player {Level=1};GC.SuppressFinalize(player);Array.Fill(player.Equipment,-1);player.Items.FillToCapacity();player.Items[1].Set(new Item(potion.Id,4));
            var activate=typeof(Player).GetMethod("ActivateExperienceBoost",BindingFlags.NonPublic|BindingFlags.Instance)!;var bonus=typeof(Player).GetProperty("PersonalExperienceBonus",BindingFlags.NonPublic|BindingFlags.Instance)!;
            Check((bool)activate.Invoke(player,[1,50,1800L])! && player.Items[1].Quantity==3 && (int)bonus.GetValue(player)! ==50,"One potion activates a timed percentage and consumes exactly one item");
            var untilId=(Guid)typeof(Player).GetField("ExperienceBoostUntilId",flags)!.GetValue(null)!;var beforeEnd=player.GetVariable(untilId)!.Value.Integer;
            Check(!(bool)activate.Invoke(player,[1,25,1800L])! && player.Items[1].Quantity==3,"A weaker potion is rejected without consumption");
            Check((bool)activate.Invoke(player,[1,50,1800L])! && player.Items[1].Quantity==2 && player.GetVariable(untilId)!.Value.Integer>beforeEnd+1799000,"Identical boost extends duration");
            var extensionEnd=player.GetVariable(untilId)!.Value.Integer;
            Check(!(bool)activate.Invoke(player,[1,50,604800L])! && player.Items[1].Quantity==2 && player.GetVariable(untilId)!.Value.Integer==extensionEnd,"A dose exceeding the duration cap must not be partially consumed");
            player.GiveExperience(10);Check(player.Exp==20,"Character XP adds the event and potion bonuses once each");
            var professions=assembly.GetType("Intersect.Server.Professions.ProfessionConfigurationRuntime",true)!;var professionCache=professions.GetField("_current",flags)!;var previousProfessions=professionCache.GetValue(null);
            var progressPath=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-boost.db");var online=(System.Collections.IDictionary)typeof(Player).GetField("OnlinePlayersById",flags)!.GetValue(null)!;
            try
            {
                var profession=new Intersect.Framework.Core.Professions.ProfessionDefinition {Name="Test Alchemist",BaseExperience=1000};professionCache.SetValue(null,new Intersect.Framework.Core.Professions.ProfessionConfiguration {Professions=[profession]});
                var professionRuntime=assembly.GetType("Intersect.Server.Professions.ProfessionRuntime",true)!;professionRuntime.GetMethod("AwardActivity",flags)!.Invoke(null,[player,profession.Id,10L]);Check((long)professionRuntime.GetMethod("GetExperience",flags)!.Invoke(null,[player,profession.Id])! ==20,"Profession XP receives the personal potion and global event once each");
                online.Add(player.Id,player);var store=new Intersect.Server.MiniGames.Progression.SqliteMiniGameProgressStore(progressPath,experienceEvents:true);var receipt=Guid.NewGuid();var won=store.AwardExperience(player.Id,"potions",receipt,1,10);Check(won.Experience==20 && store.AwardExperience(player.Id,"potions",receipt,1,10).Experience==20,"Mini-game XP receives both bonuses and receipt replay grants nothing twice");
            }
            finally{professionCache.SetValue(null,previousProfessions);online.Remove(player.Id);if(File.Exists(progressPath))File.Delete(progressPath);}

            var restored=new Player {Level=1};GC.SuppressFinalize(restored);restored.SetVariableValue(untilId,player.GetVariable(untilId)!.Value.Integer);var percentId=(Guid)typeof(Player).GetField("ExperienceBoostPercentId",flags)!.GetValue(null)!;restored.SetVariableValue(percentId,player.GetVariable(percentId)!.Value.Integer);Check((int)bonus.GetValue(restored)! ==50,"Reloaded character variables restore active potion");
            restored.SetVariableValue(untilId,DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds());Check((int)bonus.GetValue(restored)! ==0,"Expired potion cannot grant XP");
            var resource=new Resource(new ResourceDescriptor(Guid.NewGuid()){MinHp=1,MaxHp=1,Drops=[new(){ItemId=loot.Id,Chance=50,MinQuantity=2,MaxQuantity=2}]});GC.SuppressFinalize(resource);
            typeof(Resource).GetMethod("GenerateHarvestLoot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(resource,[null]);Check(resource.Items.Count==1 && resource.Items[0].Quantity==2,"Harvest uses event-time probability without changing quantities");
            config.LootEvents.Clear();
            var lootUntil=(Guid)typeof(Player).GetField("LootBoostUntilId",flags)!.GetValue(null)!;
            var lootPercent=(Guid)typeof(Player).GetField("LootBoostPercentId",flags)!.GetValue(null)!;
            restored.SetVariableValue(lootUntil,DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());restored.SetVariableValue(lootPercent,100L);
            typeof(Resource).GetMethod("GenerateHarvestLoot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(resource,[restored]);
            Check(resource.Items.Count==1 && resource.Items[0].Quantity==2,"Personal loot bonus uses the authenticated harvester without changing quantities");
            resource.Descriptor.Drops[0].Chance=0;typeof(Resource).GetMethod("GenerateHarvestLoot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(resource,[null]);Check(resource.Items.Count==0,"No stale boosted harvest remains after a fresh roll");
            var shop=new ShopDescriptor {BuybackDiscountPercent=25};Check(shop.BuybackPrice(100)==75 && shop.BuybackPrice(1)==1 && shop.BuybackPrice(0)==0,"Buyback discount is bounded and rounds safely");shop.BuybackDiscountPercent=0;Check(shop.BuybackPrice(100)==100,"Old shop prices remain unchanged");
            using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using(var command=connection.CreateCommand()){command.CommandText="CREATE TABLE Shops (Id TEXT, Name TEXT);INSERT INTO Shops VALUES ('old','Existing shop')";command.ExecuteNonQuery();}
            using var db=new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);var migration=new Intersect.Server.Migrations.Sqlite.Game.ShopBuybackDiscount();var builder=new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");migration.GetType().GetMethod("Up",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(migration,[builder]);foreach(var sql in db.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations)){using var command=connection.CreateCommand();command.CommandText=sql.CommandText;command.ExecuteNonQuery();}
            using(var command=connection.CreateCommand()){command.CommandText="SELECT Name,BuybackDiscountPercent FROM Shops";using var reader=command.ExecuteReader();Check(reader.Read() && reader.GetString(0)=="Existing shop" && reader.GetInt32(1)==0,"Migration preserves shops with no discount");}
            var wire=MessagePack.MessagePackSerializer.Deserialize<LootEventAnnouncementPacket>(MessagePack.MessagePackSerializer.Serialize(snap));Check(wire.BonusPercent==100 && wire.Silent,"Loot event packet round trip");
        }
        finally{cache.SetValue(null,previous);ItemDescriptor.Lookup.DeleteAt(potion.Id);ItemDescriptor.Lookup.DeleteAt(loot.Id);typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,oldOptions);}
        System.Console.WriteLine("PASS EXP potion activation/extension/rejection/reload/expiry, additive XP, loot schedules and actual harvest rolls, shop prices and schema migration");
    }
}
