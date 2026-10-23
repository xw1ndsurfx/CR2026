using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Dungeons;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void DungeonChestDropsRespectZeroAndFullChanceAndPersistMultipleRewards()
    {
        var drop=new DungeonChestDrop{ItemId=Guid.NewGuid(),Minimum=2,Maximum=5,Chance=0};var random=new Random(42);
        Assert.That(Enumerable.Range(0,100).Select(_=>drop.Roll(random)),Is.All.Zero);
        drop.Chance=100;Assert.That(Enumerable.Range(0,100).Select(_=>drop.Roll(random)),Is.All.InRange(2,5));
        drop.Chance=double.NaN;Assert.That(drop.IsValid,Is.False);drop.Chance=101;Assert.That(drop.IsValid,Is.False);
        drop.Chance=75;var dungeon=new DungeonDefinition{Chest=new(){Drops=[drop,new(){ItemId=Guid.NewGuid(),Chance=35}],CompletionAnimationId=Guid.NewGuid()}};
        var restored=DungeonConfiguration.FromJson(new DungeonConfiguration{Dungeons=[dungeon]}.ToJson()).Dungeons.Single();
        Assert.That(restored.Chest.Drops.Length,Is.EqualTo(2));Assert.That(restored.Chest.Drops[0].Chance,Is.EqualTo(75));
        Assert.That(restored.Chest.CompletionAnimationId,Is.EqualTo(dungeon.Chest.CompletionAnimationId));
    }
    [Test]
    public void DungeonChestCollectionChecksParticipantRangeAndInstanceAndNeverDeliversTwice()
    {
        var item=new ItemDescriptor(Guid.NewGuid()){Name="Chest reward",Stackable=true,MaxInventoryStack=99};ItemDescriptor.Lookup.Set(item.Id,item);
        var player=CargoPlayer();player.Id=Guid.NewGuid();player.EntityLock=new();player.Quests=[];
        player.Equipment=Enumerable.Repeat(-1,Options.Instance.Equipment.Slots.Count).ToArray();
        player.Items=new(Options.Instance.Player.MaxInventory,InventorySlot.Create);player.Items.FillToCapacity();
        player.Bank=new(1,BankSlot.Create);player.Bank.FillToCapacity();
        player.MapId=Guid.NewGuid();player.MapInstanceId=Guid.NewGuid();
        var flags=BindingFlags.Static|BindingFlags.NonPublic;var members=BindingFlags.Instance|BindingFlags.Public;
        var type=typeof(DungeonRunRuntime).GetNestedType("Run",BindingFlags.NonPublic)!;var run=Activator.CreateInstance(type,true)!;
        type.GetProperty("Dungeon")!.SetValue(run,new DungeonDefinition());type.GetProperty("MapInstanceId")!.SetValue(run,player.MapInstanceId);
        type.GetField("CompletionStarted",members)!.SetValue(run,true);type.GetField("ChestMap",members)!.SetValue(run,player.MapId);
        ((System.Collections.Concurrent.ConcurrentDictionary<Guid,byte>)type.GetProperty("Participants")!.GetValue(run)!)[player.Id]=0;
        ((Dictionary<Guid,(Guid,int)[]>)type.GetField("ChestLoot",members)!.GetValue(run)!)[player.Id]=[(item.Id,4)];
        var runs=(System.Collections.IDictionary)typeof(DungeonRunRuntime).GetField("RunsByInstance",flags)!.GetValue(null)!;
        var runId=player.MapInstanceId;runs.Add(runId,run);
        try
        {
            player.X=8;Assert.That(DungeonRunRuntime.ClaimChest(player,runId,persist:()=>true),Is.False);player.X=0;
            player.MapInstanceId=Guid.NewGuid();Assert.That(DungeonRunRuntime.ClaimChest(player,runId,persist:()=>true),Is.False);player.MapInstanceId=runId;
            Assert.That(DungeonRunRuntime.ClaimChest(player,runId,persist:()=>false),Is.False);
            Assert.That(player.Items.Sum(slot=>slot.Quantity),Is.Zero,"Failed persistence rolls back every grant.");
            var saves=0;Assert.That(DungeonRunRuntime.ClaimChest(player,runId,persist:()=>{saves++;return true;}),Is.True);
            Assert.That(player.Items.Single(slot=>slot.ItemId==item.Id).Quantity,Is.EqualTo(4));
            Assert.That(DungeonRunRuntime.ClaimChest(player,runId,persist:()=>{saves++;return true;}),Is.False);
            Assert.That(saves,Is.EqualTo(1));
        }
        finally{runs.Remove(runId);ItemDescriptor.Lookup.Delete(item);}
    }
}
