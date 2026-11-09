using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
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
