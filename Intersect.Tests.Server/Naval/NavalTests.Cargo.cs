using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    private static NavalCargoParcel Parcel(int quality=1)=>new(){ItemId=Guid.NewGuid(),Quantity=3,Quality=quality};
    private static Player CargoPlayer()
    {
        Options.EnsureCreated();var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(player);
        player.NavalFleetJson="[]";player.Items=new(2,InventorySlot.Create);player.Items.FillToCapacity();return player;
    }
    [Test]
    public void CargoSnapshotPreservesParcelIdentityAndQuantityAcrossTheWire()
    {
        var parcel=Parcel(4);
        var source=new Intersect.Network.Packets.Server.NavalSnapshotPacket {Ships=[new(){Cargo=[parcel],CargoCapacity=20}]};
        var copy=MessagePack.MessagePackSerializer.Deserialize<Intersect.Network.Packets.Server.NavalSnapshotPacket>(MessagePack.MessagePackSerializer.Serialize(source));
        Assert.That(copy.Ships[0].CargoCapacity,Is.EqualTo(20));
        Assert.That((copy.Ships[0].Cargo[0].Id,copy.Ships[0].Cargo[0].ItemId,copy.Ships[0].Cargo[0].Quantity),Is.EqualTo((parcel.Id,parcel.ItemId,parcel.Quantity)));
    }
    [Test]
    public void CargoCapacityReplacesOnlyWorseQualityAndRepeatedParcelsCannotDuplicate()
    {
        var cargo=new List<NavalCargoParcel>();var low=Parcel();var high=Parcel(5);
        Assert.That(NavalCargo.Add(cargo,low,1),Is.True);Assert.That(NavalCargo.Add(cargo,low,1),Is.False);
        Assert.That(NavalCargo.Add(cargo,Parcel(),1),Is.False);Assert.That(NavalCargo.Add(cargo,high,1),Is.True);
        Assert.That(cargo.Single().Id,Is.EqualTo(high.Id));Assert.That(NavalCargo.Add(cargo,Parcel(5),0),Is.False);
    }
    [Test]
    public void LootChanceAndQuantityAreRolledBeforePortAndInvalidTablesAreRejected()
    {
        var id=Guid.NewGuid();var table=new[]{new NavalLootEntry {ItemId=id,ChancePercent=50,MinimumQuantity=2,MaximumQuantity=5,Quality=4,Kind=NavalCargoKind.RoyalChest}};
        Assert.That(NavalCargo.Roll(table,()=>.6,(min,max)=>min),Is.Empty);
        var roll=NavalCargo.Roll(table,()=>.4,(min,max)=>max-1).Single();Assert.That((roll.ItemId,roll.Quantity,roll.Quality),Is.EqualTo((id,5,4)));
        table[0].ChancePercent=double.NaN;Assert.That(table[0].IsValid(),Is.False);
    }
    [Test]
    public void ShipSinkingLosesCargoOnlyWhenDamageSaveSucceeds()
    {
        var ship=new OwnedShip {Health=20,Cargo=[Parcel()]};
        Assert.That(NavalSystem.CommitOwnedHullDamage(ship,30,()=>false),Is.False);Assert.That(ship.Health,Is.EqualTo(20));Assert.That(ship.Cargo.Count,Is.EqualTo(1));
        Assert.Throws<IOException>(()=>NavalSystem.CommitOwnedHullDamage(ship,30,()=>throw new IOException()));Assert.That(ship.Cargo.Count,Is.EqualTo(1));
        Assert.That(NavalSystem.CommitOwnedHullDamage(ship,30,()=>true),Is.True);Assert.That(ship.Sunk,Is.True);Assert.That(ship.Cargo,Is.Empty);
    }
    [Test]
    public void SailingAndCargoSaveTogetherWithDurableCharacterExperienceEvenAtSailingCap()
    {
        using var world=new EnemyTestWorld();world.Configure(new(){Sailing=new(){MaximumLevel=1}});
        var player=CargoPlayer();var ship=new OwnedShip();player.NavalFleet.Add(ship);var parcel=Parcel();
        Assert.That(NavalSystem.CommitNavalReward(player,100,new[]{(ship.Id,20,parcel)},()=>false),Is.False);
        Assert.That(player.PendingNavalCharacterExperience,Is.Zero);Assert.That(player.NavalFleet[0].Cargo,Is.Empty);
        Assert.That(NavalSystem.CommitNavalReward(player,100,new[]{(ship.Id,20,parcel)},()=>true),Is.True);
        Assert.That(player.SailingExperience,Is.Zero);Assert.That(player.PendingNavalCharacterExperience,Is.EqualTo(100));Assert.That(player.NavalFleet[0].Cargo.Count,Is.EqualTo(1));
        var restored=CargoPlayer();restored.NavalFleetJson=player.NavalFleetJson;
        Assert.That(restored.PendingNavalCharacterExperience,Is.EqualTo(100));Assert.That(restored.NavalFleet[0].Cargo[0].Id,Is.EqualTo(parcel.Id));
        long earned=0;NavalSystem.DeliverCharacterExperience(restored,x=>earned+=x);NavalSystem.DeliverCharacterExperience(restored,x=>earned+=x);
        Assert.That(earned,Is.EqualTo(100));Assert.That(restored.PendingNavalCharacterExperience,Is.Zero);
    }
    [Test]
    public void CharacterExperienceRetryRetainsAnOutboxWhenNativeGrantFailsBeforeChangingThePlayer()
    {
        var player=CargoPlayer();player.PendingNavalCharacterExperience=100;
        Assert.Throws<IOException>(()=>NavalSystem.DeliverCharacterExperience(player,_=>throw new IOException()));
        Assert.That(player.PendingNavalCharacterExperience,Is.EqualTo(100));Assert.That(player.NavalCharacterRewardDirty,Is.False);
    }
    [Test]
    public void SparseInventorySnapshotKeepsRealSlotNumbersAndIncludesEmptySlots()
    {
        var player=CargoPlayer();player.Items.Clear();var item=Guid.NewGuid();player.Items[1].ItemId=item;player.Items[1].Quantity=2;
        var snapshot=NavalSystem.SnapshotCargoInventory(player);
        Assert.That(snapshot.Length,Is.EqualTo(Options.Instance.Player.MaxInventory));
        Assert.That(snapshot[0].ItemId,Is.EqualTo(Guid.Empty));Assert.That(snapshot[1].ItemId,Is.EqualTo(item));
    }
    [Test]
    public void CargoUnloadingPlansStacksAndRestoresBothCargoAndInventoryOnSaveFailure()
    {
        Options.EnsureCreated();var item=new ItemDescriptor(Guid.NewGuid()){ItemType=ItemType.Currency,Stackable=true,MaxInventoryStack=10};ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            var player=CargoPlayer();player.Items[0].Set(new Item(item.Id,8));var parcel=new NavalCargoParcel {ItemId=item.Id,Quantity=5};
            var ship=new OwnedShip {Cargo=[parcel]};player.NavalFleet.Add(ship);
            var plan=NavalSystem.PlanCargoInventory(player.Items.Select(i=>i.Clone()).ToArray(),ship.Cargo)!;
            Assert.That(plan.Select(i=>i.Quantity),Is.EqualTo(new[]{10,3}));Assert.That(player.Items[0].Quantity,Is.EqualTo(8));
            Assert.That(NavalSystem.CommitCargoUnload(player,ship,plan,()=>false),Is.False);
            Assert.That(player.Items[0].Quantity,Is.EqualTo(8));Assert.That(player.Items[1].ItemId,Is.EqualTo(Guid.Empty));Assert.That(player.NavalFleet[0].Cargo.Count,Is.EqualTo(1));
            Assert.That(NavalSystem.CommitCargoUnload(player,player.NavalFleet[0],plan,()=>true),Is.True);
            Assert.That(player.Items[0].Quantity,Is.EqualTo(10));Assert.That(player.Items[1].Quantity,Is.EqualTo(3));Assert.That(player.NavalFleet[0].Cargo,Is.Empty);
            Assert.That(NavalSystem.PlanCargoInventory(player.Items.Select(i=>i.Clone()).ToArray(),[new(){ItemId=item.Id,Quantity=Options.Instance.Player.MaxInventory*10+1}]),Is.Null);
        }
        finally {ItemDescriptor.Lookup.Delete(item);}
    }
}
