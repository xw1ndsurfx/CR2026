using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void FailedKrakenAssaultFreezesTheHullAndSchedulesSinkingAfterTheGrasp()
    {
        using var world=new EnemyTestWorld();var vessel=world.Add(12.5,12.5,false);
        var captain=GunnerPlayer(world,vessel);var state=world.State(vessel);captain.Id=state.CaptainId;
        var now=Timing.Global.Milliseconds;
        var definition=new NavalMonsterDefinition{AssaultSeconds=10};
        var battle=new NavalMonsterBattle(definition,100,now-13000);
        var type=typeof(NavalSystem).GetNestedType("EnemyBoardingRun",System.Reflection.BindingFlags.NonPublic)!;
        var run=Activator.CreateInstance(type,true)!;
        void Set(string field,object value)=>type.GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(run,value);
        Set("Captain",captain.Id);Set("Ship",state.Id);Set("Monster",definition);Set("MonsterBattle",battle);
        world.Invoke("FinishMonsterAssault",run,false,now);
        state.Speed=2;state.Throttle=1;
        world.Invoke("UpdateNavalMonsters",captain,now+3999);
        Assert.That(state.Speed,Is.Zero);Assert.That(state.Throttle,Is.Zero);
        Assert.That(world.Field<int>(vessel,"PendingDamage"),Is.Zero);
        Assert.That(world.Field<Guid>(vessel,"EnemyBoardingEncounter"),Is.Not.EqualTo(Guid.Empty));
        world.Invoke("UpdateNavalMonsters",captain,now+4000);
        Assert.That(world.Field<int>(vessel,"PendingDamage"),Is.EqualTo(100));
        Assert.That(world.Field<Guid>(vessel,"EnemyBoardingEncounter"),Is.EqualTo(Guid.Empty));
        world.Set(vessel,"PendingDamage",0);
        world.Invoke("UpdateNavalMonsters",captain,now+4500);
        Assert.That(world.Field<int>(vessel,"PendingDamage"),Is.Zero,"The fatal damage is queued only once while the hull sinks.");
    }
    [Test]
    public void TwelveLineGunnersHaveIndependentSeatsAndReplicatedAim()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var definition=world.Field<ShipDefinition>(ship,"Definition");
        NavalShipModels.Adventurers.Single(m=>m.DeckKind==ShipDeckKind.ShipOfLine).ApplyTo(definition);
        var players=Enumerable.Range(0,12).Select(_=>GunnerPlayer(world,ship)).ToArray();
        var now=Timing.Global.Milliseconds;
        var leases=new GunneryState[12];
        for(int i=0;i<12;i++)
        {
            players[i].LastGunneryRequest=now-100;
            PlaceWalker(world,ship,players[i].Id,ShipDeck.Station(i>=6,i%6,ShipDeckKind.ShipOfLine),now);
            NavalSystem.Station(players[i],new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id,Starboard=i>=6},now);
            var seat=leases[i]=(GunneryState)world.Invoke("GunnerState",players[i],now)!;
            Assert.That(seat.LeaseId,Is.Not.EqualTo(Guid.Empty),$"Seat {i}");
            Assert.That((seat.CannonsPerSide,seat.Station,seat.Starboard),Is.EqualTo((6,i%6,i>=6)));
            NavalSystem.Station(players[i],new(){Action=CannonStationAction.Renew,ShipId=seat.ShipId,LeaseId=seat.LeaseId,Starboard=i>=6,Yaw=i*6-33},now+150);
        }
        var seats=(NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+200)!;
        Assert.That(seats,Has.Length.EqualTo(12));
        Assert.That(seats.Select(s=>(s.Starboard,s.Index)).Distinct().Count(),Is.EqualTo(12));
        for(int i=0;i<12;i++)Assert.That(seats.Single(s=>s.PlayerId==players[i].Id).Yaw,Is.EqualTo(i*6-33));
        for(int i=0;i<6;i++)
        {
            Assert.That((double)world.Invoke("StationOffset",ship,i,false)!,Is.EqualTo((double)world.Invoke("StationOffset",ship,i%3,false)!));
            Assert.That(ShipDeck.Contains(ShipDeck.Station(false,i,ShipDeckKind.ShipOfLine),ShipDeckKind.ShipOfLine),Is.True);
        }
    }
}
