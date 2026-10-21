using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    private static void AddCrewPresence(EnemyTestWorld world,object ship,Guid id,long seen)
    {
        var presence=typeof(NavalSystem).GetNestedType("CrewPresence",BindingFlags.NonPublic)!;
        var member=Activator.CreateInstance(presence,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{"Sailor",seen},null)!;
        ((System.Collections.IDictionary)world.Field<object>(ship,"Crew")).Add(id,member);
    }
    [Test]
    public void CrewRewardPreservesSailingAndStillCommitsCharacterExperienceCargoAndReceiptAtomically()
    {
        using var world=new EnemyTestWorld();var player=CargoPlayer();player.SailingExperience=80;
        var ship=new OwnedShip {AtSea=true};player.NavalFleet.Add(ship);var parcel=Parcel(4);
        Assert.That(NavalSystem.CommitNavalReward(player,275,new[]{(ship.Id,20,parcel)},()=>false,out _,0),Is.False);
        Assert.That(player.SailingExperience,Is.EqualTo(80));Assert.That(player.PendingNavalCharacterExperience,Is.Zero);
        Assert.That(player.NavalFleet.Single().Cargo,Is.Empty);
        Assert.That(NavalSystem.CommitNavalReward(player,275,new[]{(ship.Id,20,parcel)},()=>true,out var receipt,0),Is.True);
        Assert.That(player.SailingExperience,Is.EqualTo(80));Assert.That(player.PendingNavalCharacterExperience,Is.EqualTo(275));
        Assert.That(receipt.SailingExperience,Is.Zero);Assert.That(receipt.Cargo.Single().Id,Is.EqualTo(parcel.Id));
    }

    [Test]
    public void BoardingRequiresCaptainConsentPartyDockStoppedShipAndCapacity()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var captain=CargoPlayer();captain.Id=world.State(ship).CaptainId;
        var member=CargoPlayer();member.Id=Guid.NewGuid();member.MapId=world.Map.Id;member.X=14;member.Y=10;member.Party=[captain,member];
        world.Map.Attributes[14,10]=new MapDockAttribute();world.Set(ship,"OwnerId",captain.Id);world.Set(ship,"BoardingOpen",true);
        Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.True);
        world.Set(ship,"BoardingOpen",false);Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.False);world.Set(ship,"BoardingOpen",true);
        member.Party=[];Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.False);member.Party=[captain,member];
        world.State(ship).Speed=1;Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.False);world.State(ship).Speed=0;
        member.X=25;Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.False);member.X=14;
        world.Field<ShipDefinition>(ship,"Definition").CrewCapacity=1;Assert.That(world.Invoke("EligibleCrewShip",member,ship),Is.False);
    }
    [Test]
    public void EveryPresentCrewMemberGetsTheSameLootAndCharacterExperienceButOnlyCaptainGetsSailing()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);
        var captain=world.State(ship).CaptainId;var crew=Guid.NewGuid();var stale=Guid.NewGuid();var now=Timing.Global.Milliseconds;
        AddCrewPresence(world,ship,crew,now);AddCrewPresence(world,ship,stale,now-3000);
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");patrol.SailingExperienceReward=275;
        var item=Guid.NewGuid();patrol.Loot=[new(){ItemId=item,MinimumQuantity=4,MaximumQuantity=4,ChancePercent=100}];
        world.Set(enemy,"Health",0);world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,captain);
        Assert.That(world.Reward(captain),Is.EqualTo(275));Assert.That(world.Reward(crew),Is.EqualTo(275));Assert.That(world.Reward(stale),Is.Zero);
        long SailingFor(Guid id)=>(long)world.Rewards[id]!.GetType().GetProperty("SailingExperience")!.GetValue(world.Rewards[id])!;
        Assert.That(SailingFor(captain),Is.EqualTo(275));Assert.That(SailingFor(crew),Is.Zero);
        NavalCargoParcel ParcelFor(Guid id)
        {
            var reward=world.Rewards[id]!;var awards=(Array)reward.GetType().GetProperty("Cargo")!.GetValue(reward)!;
            return (NavalCargoParcel)awards.GetValue(0)!.GetType().GetProperty("Parcel")!.GetValue(awards.GetValue(0))!;
        }
        var a=ParcelFor(captain);var b=ParcelFor(crew);
        Assert.That((b.ItemId,b.Quantity,b.Quality),Is.EqualTo((a.ItemId,a.Quantity,a.Quality)));Assert.That(b.Id,Is.Not.EqualTo(a.Id));
        world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,captain);Assert.That(world.Reward(crew),Is.EqualTo(275));
    }
    [Test]
    public void CrewBoardingAndPersonalRewardsPersistAndRollBackTogether()
    {
        using var world=new EnemyTestWorld();var player=CargoPlayer();var passage=new ShipCrewPassage {CaptainId=Guid.NewGuid(),Ship=new(){AtSea=true,ReturnMapId=Guid.NewGuid(),ReturnX=14,ReturnY=10}};
        Assert.That(NavalSystem.CommitCrewBoarding(player,passage,()=>false),Is.False);Assert.That(player.NavalCrewPassage,Is.Null);
        Assert.Throws<IOException>(()=>NavalSystem.CommitCrewBoarding(player,passage,()=>throw new IOException()));Assert.That(player.NavalCrewPassage,Is.Null);
        Assert.That(NavalSystem.CommitCrewBoarding(player,passage,()=>true),Is.True);
        var parcel=Parcel(4);NavalSystem.CommitNavalReward(player,275,new[]{(passage.Ship.Id,20,parcel)},()=>true,out var receipt);
        Assert.That(player.NavalCrewPassage.Ship.Cargo.Single().Id,Is.EqualTo(parcel.Id));Assert.That(receipt.Cargo.Single().Id,Is.EqualTo(parcel.Id));
        var restored=CargoPlayer();restored.NavalFleetJson=player.NavalFleetJson;
        Assert.That(restored.NavalCrewPassage.Ship.AtSea,Is.True);Assert.That(restored.NavalCrewPassage.Ship.ReturnX,Is.EqualTo(14));
        Assert.That(restored.SailingExperience,Is.EqualTo(275));Assert.That(restored.PendingNavalCharacterExperience,Is.EqualTo(275));
    }
    [Test]
    public void CrewCargoRecoveryDetectsASinkingEvenIfCaptainAlreadyRepaired()
    {
        var own=new OwnedShip {Health=100,SinkingSerial=2};var passage=new ShipCrewPassage {CaptainSinkingSerial=1,Ship=new(){Id=own.Id,Cargo=[Parcel()]}};
        NavalSystem.ReconcileCrewCargo(passage,JsonConvert.SerializeObject(new NavalPlayerData {Ships=[own]}));
        Assert.That(passage.Ship.Cargo,Is.Empty);Assert.That(passage.Ship.Sunk,Is.True);
        passage.CaptainSinkingSerial=2;passage.Ship.Health=100;passage.Ship.Cargo=[Parcel()];
        NavalSystem.ReconcileCrewCargo(passage,JsonConvert.SerializeObject(new NavalPlayerData {Ships=[own]}));Assert.That(passage.Ship.Cargo.Count,Is.EqualTo(1));
        Assert.That(NavalSystem.CommitOwnedHullDamage(own,100,()=>false),Is.False);Assert.That(own.SinkingSerial,Is.EqualTo(2));
        NavalSystem.CommitOwnedHullDamage(own,100,()=>true);Assert.That(own.SinkingSerial,Is.EqualTo(3));
    }
    [Test]
    public void CrewPacketsKeepOldIdsAndPassengerMembershipRoundTrips()
    {
        Assert.That((int)HarbourView.Fleet,Is.Zero);Assert.That((int)HarbourAction.ClaimCargo,Is.EqualTo(5));
        Assert.That(HarbourAccessRules.Allows(HarbourView.Crew,HarbourAction.JoinCrew),Is.True);
        Assert.That(HarbourAccessRules.Allows(HarbourView.Shop,HarbourAction.JoinCrew),Is.False);
        var id=Guid.NewGuid();var packet=new NavalSnapshotPacket {Crew=new(){ShipId=id,Members=[new(){PlayerId=id,Name="Sailor"}]},Ships=[new(){Id=id,CrewIds=[id]}]};
        var copy=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.Ships[0].CrewIds.Single(),Is.EqualTo(id));Assert.That(copy.Crew.Members[0].Name,Is.EqualTo("Sailor"));
        Assert.Throws<InvalidDataException>(()=>new NavalConfiguration {Ships=[new(){MapId=Guid.NewGuid(),CrewCapacity=9}]}.Validate(32,32));
    }
}
