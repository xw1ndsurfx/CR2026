using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
internal static class ActivitiesBankTests
{
    [ModuleInitializer] internal static void Run() {try{Test();}catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Test()
    {
        var friday=new DateTimeOffset(2026,10,16,23,30,0,TimeSpan.FromHours(-4));
        var evt=new ExperienceEventDefinition {Enabled=true,Days=1<<(int)DayOfWeek.Friday,StartHour=23,StartMinute=0,DurationMinutes=120,BonusPercent=50};
        var config=new WorldActivitiesConfiguration {ExperienceEvents=[evt]};
        Check(config.BonusAt(friday)==50 && config.BonusAt(friday.AddHours(1))==50 && config.BonusAt(friday.AddHours(2))==0,"XP schedule crosses midnight and ends exactly at its boundary");
        config.ExperienceEvents.Add(new ExperienceEventDefinition {Enabled=true,Days=127,StartHour=23,DurationMinutes=60,BonusPercent=100});
        Check(config.BonusAt(friday)==100 && WorldActivitiesConfiguration.AwardBonus(101,50)==51,"Overlapping XP events use the strongest bonus and round deterministically");
        using(var connection=new SqliteConnection("Data Source=:memory:"))
        {
            connection.Open();
            using(var command=connection.CreateCommand()){command.CommandText="CREATE TABLE Players (Id TEXT PRIMARY KEY, Name TEXT); INSERT INTO Players VALUES ('existing','Preserved');";command.ExecuteNonQuery();}
            using var context=new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
            var migration=new Intersect.Server.Migrations.Sqlite.Player.PlayerBankAccount();var builder=new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            migration.GetType().GetMethod("Up",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(migration,[builder]);
            foreach(var sql in context.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations)){using var command=connection.CreateCommand();command.CommandText=sql.CommandText;command.ExecuteNonQuery();}
            using(var command=connection.CreateCommand()){command.CommandText="SELECT Name,BankAccount FROM Players";using var reader=command.ExecuteReader();Check(reader.Read() && reader.GetString(0)=="Preserved" && reader.IsDBNull(1),"Bank account migration preserves existing characters and starts with a null account profile");}
        }
        var old=Options.Instance;typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options());
        var descriptor=new ItemDescriptor(Guid.NewGuid()) {Name="Bank test",Stackable=true,CanBank=true,MaxInventoryStack=100,MaxBankStack=100};ItemDescriptor.Lookup[descriptor.Id]=descriptor;
        try
        {
            var player=new Player();GC.SuppressFinalize(player);
            player.BankAccount=new BankAccountProfile {Opened=true,BaseSlots=2,PremiumGranted=true};
            player.Bank.Capacity=4;player.Bank.FillToCapacity();player.Items.FillToCapacity();
            player.Bank[2].Set(new Item(descriptor.Id,3));player.Items[0].Set(new Item(descriptor.Id,2));
            var bank=new BankInterface<BankSlot>(player,player.Bank);player.BankInterface=bank;
            Check(!player.HasBankPremium && !bank.TryDepositItem(null,0,1,2),"Expired Premium cannot deposit into its reserved tab");
            Check(player.Bank[2].Quantity==3 && player.Items[0].Quantity==2,"Rejected premium deposit preserves both inventories");
            Check(bank.TryWithdrawItem(null,2,1) && player.Bank[2].Quantity==2,"Premium expiry leaves stored items withdrawable");
            Check(!bank.SortItems(),"Automatic sorting is enforced as Premium-only on the server");
            var currency=new ItemDescriptor(Guid.NewGuid()) {Name="Auragems",Stackable=true,CanBank=true,MaxInventoryStack=100,MaxBankStack=100};ItemDescriptor.Lookup[currency.Id]=currency;
            try
            {
                var fresh=new Player();GC.SuppressFinalize(fresh);fresh.Level=14;fresh.Items.FillToCapacity();fresh.Items[0].Set(new Item(currency.Id,5));
                var ensure=typeof(Player).GetMethod("EnsureBankAccess",BindingFlags.NonPublic|BindingFlags.Instance)!;
                Check(!(bool)ensure.Invoke(fresh,null)! && fresh.Items[0].Quantity==5 && !fresh.BankAccount.Opened,"A failed account checkpoint rolls back the opening fee and account flag together");
            }
            finally {ItemDescriptor.Lookup.DeleteAt(currency.Id);}
            player.Level=14;
            Check((bool)typeof(Player).GetMethod("EnsureBankAccess",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(player,null)!,"Previously opened accounts remain accessible without another opening debit");
            player.Level=13;
            Check(!(bool)typeof(Player).GetMethod("EnsureBankAccess",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(player,null)!,"The level 14 requirement is enforced server-side");
            var saved=player.BankAccountJson;player.BankAccountJson=saved;
            Check(player.BankAccount.Opened&&player.BankAccount.PremiumGranted&&player.BaseBankSlots==2,"Account opening and premium capacity persist without a second fee");
        }
        finally{ItemDescriptor.Lookup.DeleteAt(descriptor.Id);typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,old);}
        System.Console.WriteLine("PASS scheduled XP overlap/midnight boundaries and real bank expiry deposit/withdraw/sort enforcement");
    }
}
