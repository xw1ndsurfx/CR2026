using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void SixPlayersCanAimInDifferentDirectionsWithoutChangingAnotherStation()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        double[] angles=[-30,25,-15,40,-35,10];
        var players=Enumerable.Range(0,6).Select(_=>GunnerPlayer(world,ship)).ToArray();
        var now=Timing.Global.Milliseconds;
        foreach(var player in players)player.LastGunneryRequest=now-100;
        var states=new GunneryState[6];
        for(var i=0;i<6;i++)
        {
            NavalSystem.Station(players[i],new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id,Starboard=i>=3},now);
            states[i]=(GunneryState)world.Invoke("GunnerState",players[i],now)!;
            Assert.That(states[i].Station,Is.EqualTo(i%3));
            NavalSystem.Station(players[i],new(){Action=CannonStationAction.Renew,ShipId=states[i].ShipId,LeaseId=states[i].LeaseId,Starboard=i>=3,Yaw=angles[i]},now+150);
        }
        NavalCrewStation[] Seats() => (NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+400)!;
        var seats=Seats();Assert.That(seats,Has.Length.EqualTo(6));
        Assert.That(seats.Select(s=>(s.Starboard,s.Index)).Distinct().Count(),Is.EqualTo(6));
        for(var i=0;i<6;i++)Assert.That(seats.Single(s=>s.PlayerId==players[i].Id).Yaw,Is.EqualTo(angles[i]));
        NavalSystem.Station(players[0],new(){Action=CannonStationAction.Renew,ShipId=states[0].ShipId,LeaseId=states[0].LeaseId,Yaw=12},now+350);
        seats=Seats();Assert.That(seats.Single(s=>s.PlayerId==players[0].Id).Yaw,Is.EqualTo(12));
        for(var i=1;i<6;i++)Assert.That(seats.Single(s=>s.PlayerId==players[i].Id).Yaw,Is.EqualTo(angles[i]));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(200)]
    public void InvalidOrForeignAimCannotOverwriteTheAuthoritativeSeat(double invalid)
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var player=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        NavalSystem.Station(player,new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id,Starboard=true},now);
        var state=(GunneryState)world.Invoke("GunnerState",player,now)!;
        var packet=new Intersect.Network.Packets.Client.CannonStationPacket {Action=CannonStationAction.Renew,ShipId=state.ShipId,LeaseId=state.LeaseId,Starboard=true,Yaw=24};
        NavalSystem.Station(player,packet,now+150);
        NavalCrewStation Seat() => ((NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+200)!)[0];
        Assert.That(Seat().Yaw,Is.EqualTo(24));
        packet.Yaw=invalid;NavalSystem.Station(player,packet,now+300);
        Assert.That(Seat().Yaw,Is.EqualTo(24));
        packet.Yaw=-10;packet.LeaseId=Guid.NewGuid();NavalSystem.Station(player,packet,now+450);
        Assert.That(Seat().Yaw,Is.EqualTo(24));
        packet.LeaseId=state.LeaseId;packet.Starboard=false;NavalSystem.Station(player,packet,now+600);
        Assert.That(Seat().Yaw,Is.EqualTo(24));
        packet.Starboard=true;NavalSystem.Station(player,packet,now+750);
        Assert.That(Seat().Yaw,Is.EqualTo(-10));
        Assert.That(player.LastGunneryRequest,Is.EqualTo(now),"Aim renewals must not consume the fire/action rate limit.");
        packet.Action=CannonStationAction.Fire;
        NavalSystem.Station(player,packet,now+800);
        var balls=(System.Collections.IList)typeof(NavalSystem).GetField("Cannonballs",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.That(balls.Count,Is.EqualTo(1),"A valid shot 50 ms after an aim update must still fire.");
        packet.Action=CannonStationAction.Release;
        NavalSystem.Station(player,packet,now+810);
        Assert.That((NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+820)!,Is.Empty,"Closing the cannon immediately after firing must release the station without waiting for the fire rate limit.");
        Assert.That((NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+4500)!,Is.Empty);
    }

    [Test]
    public void DeckCrewSnapshotUsesLiveAuthoritativeSeatsAndExpiresWithTheLease()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var player=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        NavalSystem.Station(player,new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id,Starboard=true},now);
        var gunner=(GunneryState)world.Invoke("GunnerState",player,now)!;
        var seats=(NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now)!;
        Assert.That(seats,Has.Length.EqualTo(1));
        Assert.That(seats[0].PlayerId,Is.EqualTo(player.Id));
        Assert.That(seats[0].Starboard,Is.True);
        Assert.That(seats[0].Index,Is.EqualTo(gunner.Station));
        Assert.That((NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now+4000)!,Is.Empty);
    }
}
