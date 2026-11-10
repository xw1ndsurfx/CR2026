using System.Collections;
using System.Reflection;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [TestCase("adventurer-sloop","adventurer-raft",32,26)]
    [TestCase("adventurer-raft","adventurer-sloop",32,26)]
    [TestCase("adventurer-sloop","adventurer-raft",24,20)]
    [TestCase("adventurer-raft","adventurer-sloop",24,20)]
    public void SmallShipBoardingKeepsBothRealDecksAndEveryEnemyReachable(string from,string to,int width,int height)
    {
        var a=NavalShipModels.All.Single(m=>m.Id==from);var b=NavalShipModels.All.Single(m=>m.Id==to);
        var scene=NavalBoardingScene.Create(a.Graphics,b.Graphics,width,height);
        Assert.That(scene,Is.Not.Null,$"{from} to {to}");
        var queue=new Queue<(int X,int Y)>();var reached=new HashSet<(int,int)>();queue.Enqueue((scene!.EntryX,scene.EntryY));
        while(queue.TryDequeue(out var p))
        {
            if(!scene.Walkable(p.X,p.Y) || !reached.Add(p))continue;
            foreach(var next in new[]{(p.X+1,p.Y),(p.X-1,p.Y),(p.X,p.Y+1),(p.X,p.Y-1)})queue.Enqueue(next);
        }
        var crew=scene.CrewPositions(b.CrewCapacity);Assert.That(crew,Has.Length.EqualTo(b.CrewCapacity));
        foreach(var p in crew)Assert.That(reached.Contains(p),Is.True,$"Disconnected enemy at {p} in {from} to {to}");
        Assert.That(scene.Ships[0].Graphics,Is.EqualTo(a.IdleGraphics));
        Assert.That(scene.Ships[1].Graphics,Is.EqualTo(b.IdleGraphics));
    }
    [TestCase("royal-sloop",3,2)]
    [TestCase("adventurer-raft",2,1)]
    public void SmallShipsExposeOnlyTheirRealStationsAndFireFromTheCorrectSide(string id,int capacity,int stations)
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var model=NavalShipModels.All.Single(m=>m.Id==id);world.Field<ShipDefinition>(ship,"Definition").Graphics=model.Graphics;
        Assert.That(world.Invoke("VesselCapacity",ship),Is.EqualTo(capacity));
        var players=Enumerable.Range(0,stations).Select(_=>GunnerPlayer(world,ship)).ToArray();
        var balls=(IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var now=Intersect.Framework.Core.Timing.Global.Milliseconds;
        for(int i=0;i<stations;i++)
        {
            var starboard=i==1;var player=players[i];
            PlaceWalker(world,ship,player.Id,ShipDeck.Station(starboard,0,model.DeckKind),now);
            NavalSystem.DeckInput(player,new(){ShipId=world.State(ship).Id,Action=DeckAction.Interact},now);
            var gun=(GunneryState)world.Invoke("GunnerState",player,now)!;
            Assert.That(gun.LeaseId,Is.Not.EqualTo(Guid.Empty));Assert.That(gun.Station,Is.Zero);
            Assert.That(gun.Starboard,Is.EqualTo(starboard));Assert.That(gun.CannonsPerSide,Is.EqualTo(1));
            player.LastGunneryRequest=now-100;
            NavalSystem.Station(player,new(){ShipId=gun.ShipId,LeaseId=gun.LeaseId,Starboard=starboard,Action=CannonStationAction.Fire,Yaw=20,Elevation=-5},now);
            Assert.That(balls.Count,Is.EqualTo(i+1),"One occupied small-ship station fires one projectile");
            var ball=balls[i]!;
            var heading=(double)ball.GetType().GetField("Heading",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(ball)!;
            Assert.That(heading,Is.EqualTo(ShipMovement.Normalize(ShipDeck.CannonHeading(world.State(ship).Heading,starboard,model.DeckKind)+20)).Within(.0001));
        }
        var snapshot=(NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now)!;
        Assert.That(snapshot,Has.Length.EqualTo(stations));Assert.That(snapshot.All(s=>s.Index==0),Is.True);
        if(model.DeckKind==ShipDeckKind.Raft)
        {
            var gun=(GunneryState)world.Invoke("GunnerState",players[0],now)!;players[0].LastGunneryRequest=now-100;
            NavalSystem.Station(players[0],new(){ShipId=gun.ShipId,Action=CannonStationAction.Acquire,Starboard=true},now);
            Assert.That(((GunneryState)world.Invoke("GunnerState",players[0],now)!).LeaseId,Is.EqualTo(gun.LeaseId),"A nonexistent second side cannot replace the raft's only reservation");
        }
    }
    [Test]
    public void SmallDeckCollisionsDoNotKeepPhantomMastsOrAllowWalkingThroughTheirOwnGun()
    {
        Assert.That(ShipDeck.Contains(new(0,-2.5)),Is.False);
        Assert.That(ShipDeck.Contains(new(0,-2.5),ShipDeckKind.Sloop),Is.True);
        Assert.That(ShipDeck.Contains(new(0,0),ShipDeckKind.Sloop),Is.False);
        Assert.That(ShipDeck.Contains(new(0,0),ShipDeckKind.Raft),Is.True);
        Assert.That(ShipDeck.Contains(new(0,2.5),ShipDeckKind.Raft),Is.False);
        foreach(var kind in new[]{ShipDeckKind.Sloop,ShipDeckKind.Raft})
        {
            Assert.That(ShipDeck.Contains(ShipDeck.Helm,kind),Is.True);
            var p=new DeckPoint(.7,1);
            for(int tick=0;tick<100;tick++)p=ShipDeck.Move(p,1,0,.1,kind:kind);
            Assert.That(ShipDeck.Contains(p,kind),Is.True);Assert.That(p.X,Is.LessThan(1.12));
        }
    }
}
