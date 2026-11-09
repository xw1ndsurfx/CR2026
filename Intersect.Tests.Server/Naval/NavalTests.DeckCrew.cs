using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
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
