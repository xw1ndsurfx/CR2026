using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void EnemyDeckSnapshotUsesEachChosenNpcAndKeepsGunRolesIndependent()
    {
        using var world=new EnemyTestWorld();var enemy=world.Add(13.5,10.5,true);
        world.Field<ShipDefinition>(enemy,"Definition").Graphics=NavalShipModels.ThreeMasts[1].Graphics;
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");
        var npcs=Enumerable.Range(0,7).Select(i=>new NPCDescriptor(Guid.NewGuid()){Sprite="sailor-"+i+".png",Name="Sailor "+i}).ToArray();
        try
        {
            foreach(var npc in npcs)NPCDescriptor.Lookup.Set(npc.Id,npc);
            patrol.DeckCaptainNpcId=npcs[0].Id;patrol.PortGunner1NpcId=npcs[1].Id;patrol.PortGunner2NpcId=npcs[2].Id;patrol.PortGunner3NpcId=npcs[3].Id;
            patrol.StarboardGunner1NpcId=npcs[4].Id;patrol.StarboardGunner2NpcId=npcs[5].Id;patrol.StarboardGunner3NpcId=npcs[6].Id;
            world.Set(enemy,"EnemyHasTarget",true);world.Set(enemy,"EnemyTargetX",20d);world.Set(enemy,"EnemyTargetY",12d);
            var snapshot=(NavalNpcDeckMember[])world.Invoke("NpcDeckSnapshot",enemy)!;
            Assert.That(snapshot,Has.Length.EqualTo(7));Assert.That(snapshot.Select(n=>n.NpcId),Is.EqualTo(npcs.Select(n=>n.Id)));
            Assert.That(snapshot.Count(n=>n.Role==ShipCrewRole.Helm),Is.EqualTo(1));
            Assert.That(snapshot.Count(n=>n.Role==ShipCrewRole.PortCannon),Is.EqualTo(3));Assert.That(snapshot.Count(n=>n.Role==ShipCrewRole.StarboardCannon),Is.EqualTo(3));
            Assert.That(snapshot.Select(n=>(n.Role,n.Index)).Distinct().Count(),Is.EqualTo(7));
            Assert.That(snapshot.All(n=>double.IsFinite(n.Yaw) && Math.Abs(n.Yaw)<=45),Is.True);
            world.Set(enemy,"SunkUntil",1000L);Assert.That((NavalNpcDeckMember[])world.Invoke("NpcDeckSnapshot",enemy)!,Is.Empty);
        }
        finally{foreach(var npc in npcs)NPCDescriptor.Lookup.DeleteAt(npc.Id);}
    }
    [Test]
    public void EnemyBoardingRequiresOptInWeakenedPlayerHullAndRange()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(13.5,10.5,true);
        world.Set(ship,"OwnerId",world.State(ship).CaptainId);world.Set(ship,"Health",25);
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");
        Assert.That(world.Invoke("CanEnemyBoardPlayer",enemy,ship),Is.False);
        patrol.CanBoardPlayerShips=true;Assert.That(world.Invoke("CanEnemyBoardPlayer",enemy,ship),Is.True);
        world.Set(ship,"Health",26);Assert.That(world.Invoke("CanEnemyBoardPlayer",enemy,ship),Is.False);
        world.Set(ship,"Health",25);world.State(enemy).X=28;Assert.That(world.Invoke("CanEnemyBoardPlayer",enemy,ship),Is.False);
        world.State(enemy).X=13.5;world.Set(ship,"SunkUntil",1000L);Assert.That(world.Invoke("CanEnemyBoardPlayer",enemy,ship),Is.False);
    }
    [Test]
    public void DamageZonesAppearFromHullDamageAndDisappearAfterRepair()
    {
        var ship=new ShipDefinition {Graphics=NavalShipModels.ThreeMasts[0].Graphics};
        Assert.That(ShipDamageZone.Active(ship,100,100),Is.Empty);
        var small=ShipDamageZone.Active(ship,99,100).ToArray();Assert.That(small,Has.Length.EqualTo(1));Assert.That(small[0].IsValid(),Is.True);
        Assert.That(ShipDamageZone.Active(ship,30,100).Count(),Is.GreaterThan(small.Length));
        ship.DamageZones=[new(){Id="fire",X=.5,Y=1.4,HealthThreshold=60,DamageAnimationId=Guid.NewGuid()}];
        Assert.That(ShipDamageZone.Active(ship,59,100).Single().Id,Is.EqualTo("fire"));
        Assert.That(ShipDamageZone.Active(ship,60,100),Is.Empty);
        Assert.That(ShipDamageZone.Active(ship,0,100),Is.Empty);
        ship.DamageZones=[];ship.UseDefaultDamageZones=false;Assert.That(ShipDamageZone.Active(ship,1,100),Is.Empty);
    }
    [Test]
    public void APhysicalRepairRequiresProximityAndReservesOnlyItsOwnDamagedZone()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var definition=world.Field<ShipDefinition>(ship,"Definition");
        definition.DamageZones=[new(){Id="deck-fire",X=.5,Y=-1.4,HealthThreshold=80,DamageAnimationId=Guid.NewGuid()}];
        world.Set(ship,"Health",50);
        var first=GunnerPlayer(world,ship);var second=GunnerPlayer(world,ship);var now=Intersect.Framework.Core.Timing.Global.Milliseconds;
        var repairs=(System.Collections.IDictionary)typeof(Intersect.Server.Naval.NavalSystem).GetField("Repairs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        try
        {
            PlaceWalker(world,ship,first.Id,new(-.5,1.4),now);
            Assert.That(world.Invoke("TryStartDeckRepair",ship,first,now,false),Is.False);Assert.That(repairs.Contains(first.Id),Is.False);
            PlaceWalker(world,ship,first.Id,new(.5,-1.4),now);
            Assert.That(world.Invoke("TryStartDeckRepair",ship,first,now,false),Is.True);Assert.That(repairs.Contains(first.Id),Is.True);
            var round=repairs[first.Id]!;
            Assert.That(round.GetType().GetField("ZoneId",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(round),Is.EqualTo("deck-fire"));
            PlaceWalker(world,ship,second.Id,new(.25,-1.4),now);
            Assert.That(world.Invoke("TryStartDeckRepair",ship,second,now,false),Is.True);
            Assert.That(repairs.Contains(second.Id),Is.False,"A second player cannot start another puzzle for an occupied hole");
            repairs.Remove(first.Id);world.Set(ship,"Health",80);
            Assert.That(world.Invoke("TryStartDeckRepair",ship,first,now,false),Is.False,"A repaired zone no longer offers a repair interaction");
        }
        finally{repairs.Remove(first.Id);repairs.Remove(second.Id);}
    }
    [TestCase(32,26)]
    [TestCase(24,20)]
    public void BoardingSceneHasTwoRealDecksAndAConnectedPassageToEveryEnemy(int width,int height)
    {
        var scene=NavalBoardingScene.Create(NavalShipModels.ThreeMasts[0].Graphics,NavalShipModels.ThreeMasts[1].Graphics,width,height);
        Assert.That(scene,Is.Not.Null);Assert.That(scene!.Ships,Has.Length.EqualTo(2));
        var reached=new HashSet<(int,int)>();var queue=new Queue<(int X,int Y)>();queue.Enqueue((scene.EntryX,scene.EntryY));
        while(queue.TryDequeue(out var p))
        {
            if(!scene.Walkable(p.X,p.Y) || !reached.Add(p))continue;
            foreach(var next in new[]{(p.X+1,p.Y),(p.X-1,p.Y),(p.X,p.Y+1),(p.X,p.Y-1)})queue.Enqueue(next);
        }
        var crew=scene.CrewPositions(7);Assert.That(crew,Has.Length.EqualTo(7));Assert.That(crew.Distinct().Count(),Is.EqualTo(7));
        foreach(var p in crew)Assert.That(reached.Contains(p),Is.True,"Every enemy station can be reached from the player's deck over the gangplank.");
        Assert.That(scene.Walkable(0,0),Is.False);Assert.That(scene.Walkable(width-1,height-1),Is.False);
        Assert.That(scene.Ships[0].Graphics,Is.EqualTo(NavalShipModels.ThreeMasts[0].IdleGraphics));
        var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<NavalBoardingScene>(Newtonsoft.Json.JsonConvert.SerializeObject(scene))!;
        Assert.That(copy.CrewPositions(7),Is.EqualTo(crew));Assert.That(copy.Walkable(copy.EntryX,copy.EntryY),Is.True);
    }
    [Test]
    public void FishingStationsRequireAnIdleBoatProximityAndAnOwnedReservation()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var definition=world.Field<ShipDefinition>(ship,"Definition");definition.DeckActivity=ShipDeckActivity.Fishing;definition.FishingZoneId=Guid.NewGuid();
        var player=GunnerPlayer(world,ship);var other=GunnerPlayer(world,ship);var now=Intersect.Framework.Core.Timing.Global.Milliseconds;
        PlaceWalker(world,ship,player.Id,new(-.78,-2),now);
        world.State(ship).Speed=1;Assert.That(world.Invoke("ReserveDeckFishing",ship,player,now),Is.Null);
        world.State(ship).Speed=0;world.State(ship).Throttle=0;
        Assert.That(world.Invoke("ReserveDeckFishing",ship,player,now),Is.Not.Null);
        Assert.That(Intersect.Server.Naval.NavalSystem.ValidFishingStation(player,world.State(ship).Id),Is.True);
        Assert.That(Intersect.Server.Naval.NavalSystem.ValidFishingStation(other,world.State(ship).Id),Is.False);
        Assert.That(((GunneryState)world.Invoke("GunnerState",player,now)!).Enabled,Is.False);
        Assert.That((NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now)!,Is.Empty,"Fishing reservations are not exposed as gun leases");
        world.State(ship).Speed=.1;Assert.That(Intersect.Server.Naval.NavalSystem.ValidFishingStation(player,world.State(ship).Id),Is.False);
    }
}
