using System.Collections;
using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void SixPlayersWalkToFreeStationsOnATurningMovingShipAndKeepTheirOwnAim()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var players=Enumerable.Range(0,6).Select(_=>GunnerPlayer(world,ship)).ToArray();
        var state=world.State(ship);state.Speed=3;var definition=world.Field<ShipDefinition>(ship,"Definition");
        var now=Timing.Global.Milliseconds;var frames=new List<ShipSnapshot>();double[] angles=[-30,25,-15,40,-35,10];
        world.Invoke("EnsureDeck",ship,now);
        void Tick()
        {
            now+=100;
            foreach(var p in players){((IDictionary)world.Field<object>(ship,"Crew")).Remove(p.Id);AddCrewPresence(world,ship,p.Id,now);}
            ShipMovement.Step(state,definition,new(1,1,false),.1,(map,x,y,heading)=>(true,map,x,y));
            for(var i=0;i<6;i++)
            {
                var gun=(GunneryState)world.Invoke("GunnerState",players[i],now)!;
                if(gun.LeaseId!=Guid.Empty)NavalSystem.Station(players[i],new(){Action=CannonStationAction.Renew,ShipId=state.Id,LeaseId=gun.LeaseId,Starboard=gun.Starboard,Yaw=angles[i]},now);
            }
        }
        void Capture()
        {
            var snapshot=new ShipSnapshot {Id=state.Id,CaptainId=state.CaptainId,HelmsmanId=state.CaptainId,MapId=state.MapId,
                X=state.X,Y=state.Y,Heading=state.Heading,Speed=state.Speed,Graphics=definition.Graphics,Frames=4,FrameMilliseconds=180,
                WalkableDeck=true,CrewIds=players.Select(p=>p.Id).ToArray(),DeckMembers=Walkers(world,ship,now),
                CrewStations=(NavalCrewStation[])world.Invoke("CrewStationSnapshot",ship,now)!};
            frames.Add(MessagePack.MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePack.MessagePackSerializer.Serialize(snapshot)));
        }
        Capture();
        for(var actor=0;actor<6;actor++)
        {
            var current=Walkers(world,ship,now);var me=current.Single(p=>p.PlayerId==players[actor].Id);
            var goal=ShipDeck.Station(actor>=3,actor%3);
            var others=current.Where(p=>p.PlayerId!=me.PlayerId).Select(p=>new DeckPoint(p.X,p.Y)).ToArray();
            var route=DeckRoute(new(me.X,me.Y),goal,others);
            Assert.That(route,Is.Not.Empty,"Each of the six stations must be reachable without crossing a mast or another player.");
            foreach(var waypoint in route)
            {
                Tick();me=Walkers(world,ship,now).Single(p=>p.PlayerId==players[actor].Id);
                var dx=waypoint.X-me.X;var dy=waypoint.Y-me.Y;
                var gain=Math.Max(1,Math.Sqrt(dx*dx+dy*dy)/(ShipDeck.Speed*.1));
                NavalSystem.DeckInput(players[actor],new(){ShipId=state.Id,Side=(sbyte)Math.Clamp(Math.Round(dx/(ShipDeck.Speed*.1)/gain*127),-127,127),Forward=(sbyte)Math.Clamp(Math.Round(dy/(ShipDeck.Speed*.1)/gain*127),-127,127)},now);
                me=Walkers(world,ship,now).Single(p=>p.PlayerId==players[actor].Id);
                Assert.That(ShipDeck.Distance(new(me.X,me.Y),waypoint),Is.LessThan(.06));
                Assert.That(ShipDeck.Contains(new(me.X,me.Y)),Is.True);Capture();
            }
            Tick();NavalSystem.DeckInput(players[actor],new(){ShipId=state.Id,Action=DeckAction.Interact},now);
            var taken=(GunneryState)world.Invoke("GunnerState",players[actor],now)!;
            Assert.That(taken.LeaseId,Is.Not.EqualTo(Guid.Empty));Assert.That(taken.Starboard,Is.EqualTo(actor>=3));Assert.That(taken.Station,Is.EqualTo(actor%3));
            Capture();
        }
        Tick();Capture();
        var last=frames[^1];Assert.That(last.DeckMembers,Has.Length.EqualTo(7));Assert.That(last.CrewStations,Has.Length.EqualTo(6));
        for(var i=0;i<6;i++)Assert.That(last.CrewStations.Single(s=>s.PlayerId==players[i].Id).Yaw,Is.EqualTo(angles[i]));
        Assert.That(frames.Select(f=>ShipMovement.DirectionRow(f.Heading)).Distinct().Count(),Is.GreaterThan(1));
        Assert.That(ShipDeck.Distance(new(frames[0].X,frames[0].Y),new(last.X,last.Y)),Is.GreaterThan(.1));
        File.WriteAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"deck-walk-replay.json"),Newtonsoft.Json.JsonConvert.SerializeObject(frames));
    }
    private static DeckPoint[] DeckRoute(DeckPoint start,DeckPoint goal,DeckPoint[] others)
    {
        const int width=21,height=71;var nodes=new DeckPoint[width*height];var free=new bool[nodes.Length];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var p=new DeckPoint((x-10)/10d,(y-35)/10d);var index=y*width+x;nodes[index]=p;
            free[index]=ShipDeck.Contains(p) && others.All(o=>ShipDeck.Distance(p,o)>=ShipDeck.Radius*2+.015);
        }
        var first=Enumerable.Range(0,nodes.Length).Where(i=>free[i]).OrderBy(i=>ShipDeck.Distance(nodes[i],start)).First();
        var before=Enumerable.Repeat(-2,nodes.Length).ToArray();before[first]=-1;var queue=new Queue<int>();queue.Enqueue(first);int found=-1;
        while(queue.TryDequeue(out var i))
        {
            if(ShipDeck.Distance(nodes[i],goal)<=.28){found=i;break;}
            foreach(var next in new[]{i-1,i+1,i-width,i+width})
            {
                if(next<0 || next>=nodes.Length || before[next]!=-2 || !free[next] || ShipDeck.Distance(nodes[i],nodes[next])>.101)continue;
                var a=nodes[i];var b=nodes[next];var reached=ShipDeck.Move(a,(b.X-a.X)/.165,(b.Y-a.Y)/.165,.1,p=>others.Any(o=>ShipDeck.Distance(p,o)<ShipDeck.Radius*2));
                if(ShipDeck.Distance(reached,b)>.001)continue;
                before[next]=i;queue.Enqueue(next);
            }
        }
        if(found<0)return [];
        var result=new List<DeckPoint>();for(int i=found;i>=0;i=before[i])result.Add(nodes[i]);result.Reverse();return result.ToArray();
    }
    private static void EnableWalking(EnemyTestWorld world,object ship)
    {
        world.Field<ShipDefinition>(ship,"Definition").Graphics="Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png";
    }
    [Test]
    public void RoyalCrewPartyCanReachSevenWithoutChangingOrdinaryPartyLimits()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var crew=GunnerPlayer(world,ship);var captain=crew.Party.Single(p=>p.Id==world.State(ship).CaptainId);
        captain.Party=crew.Party;
        var configured=Options.Instance.Party.MaximumMembers;
        Options.Instance.Party.MaximumMembers=5;
        try
        {
            world.Invoke("EnsureDeck",ship,Timing.Global.Milliseconds);
            Assert.That(NavalSystem.PartyCapacity(crew),Is.EqualTo(7));
            Assert.That(NavalSystem.PartyCapacity(captain),Is.EqualTo(7));
            var unrelated=CargoPlayer();unrelated.Party=[];
            Assert.That(NavalSystem.PartyCapacity(unrelated),Is.EqualTo(5));
            world.Field<ShipDefinition>(ship,"Definition").Graphics="legacy-ship.png";
            Walkers(world,ship,Timing.Global.Milliseconds);
            Assert.That(NavalSystem.PartyCapacity(crew),Is.EqualTo(5));
        }
        finally {Options.Instance.Party.MaximumMembers=configured;}
    }
    private static NavalDeckMember[] Walkers(EnemyTestWorld world,object ship,long now)=>(NavalDeckMember[])world.Invoke("DeckSnapshot",ship,now)!;
    private static void PlaceWalker(EnemyTestWorld world,object ship,Guid id,DeckPoint point,long now)
    {
        world.Invoke("EnsureDeck",ship,now);
        var value=((IDictionary)world.Field<object>(ship,"Deck"))[id]!;
        value.GetType().GetField("Position",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(value,point);
    }
    [Test]
    public void WalkingIsBoundedByElapsedTimeAndCannotBeDrivenByForgedOrRepeatedPackets()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var player=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        PlaceWalker(world,ship,player.Id,new(-.5,-1.3),now);
        DeckPoint Position(){var p=Walkers(world,ship,now+100).Single(p=>p.PlayerId==player.Id);return new(p.X,p.Y);}
        var packet=new DeckInputPacket {ShipId=world.State(ship).Id,Side=127};var start=Position();
        NavalSystem.DeckInput(player,packet,now+100);var moved=Position();
        Assert.That(ShipDeck.Distance(start,moved),Is.EqualTo(ShipDeck.Speed*.1).Within(.000001));
        for(var i=0;i<500;i++)NavalSystem.DeckInput(player,packet,now+100);
        Assert.That(Position(),Is.EqualTo(moved));
        packet.ShipId=Guid.NewGuid();NavalSystem.DeckInput(player,packet,now+200);
        Assert.That(Position(),Is.EqualTo(moved));
        packet.ShipId=world.State(ship).Id;packet.Side=-128;NavalSystem.DeckInput(player,packet,now+300);
        Assert.That(Position(),Is.EqualTo(moved));
        ((IDictionary)world.Field<object>(ship,"Crew")).Remove(player.Id);AddCrewPresence(world,ship,player.Id,now+10000);
        packet.Side=127;NavalSystem.DeckInput(player,packet,now+10000);
        Assert.That(ShipDeck.Distance(Position(),moved),Is.LessThanOrEqualTo(ShipDeck.Speed*.1+.000001));
    }
    [Test]
    public void WalkingCannotCrossTheRailOrTheCentreMast()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var player=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        PlaceWalker(world,ship,player.Id,new(.85,1),now);
        var packet=new DeckInputPacket {ShipId=world.State(ship).Id,Side=127};
        for(var i=1;i<=8;i++)NavalSystem.DeckInput(player,packet,now+i*100);
        var p=Walkers(world,ship,now+800).Single(p=>p.PlayerId==player.Id);
        Assert.That(p.X,Is.LessThanOrEqualTo(1.000001));Assert.That(ShipDeck.Contains(new(p.X,p.Y)),Is.True);
        PlaceWalker(world,ship,player.Id,new(-.6,0),now+800);
        for(var i=9;i<=14;i++)NavalSystem.DeckInput(player,packet,now+i*100);
        p=Walkers(world,ship,now+1400).Single(p=>p.PlayerId==player.Id);
        Assert.That(p.X,Is.LessThanOrEqualTo(-.35));
    }
    [Test]
    public void OnlyANearbyFreeCannonCanBeClaimedAndReleasingItLetsAnotherPlayerTakeIt()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var first=GunnerPlayer(world,ship);var second=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        first.LastGunneryRequest=second.LastGunneryRequest=now-100;
        PlaceWalker(world,ship,first.Id,new(-.5,-1.3),now);
        NavalSystem.Station(first,new(){ShipId=world.State(ship).Id,Action=CannonStationAction.Acquire,Starboard=true},now);
        Assert.That(((GunneryState)world.Invoke("GunnerState",first,now)!).LeaseId,Is.EqualTo(Guid.Empty),"Opening the cannon UI must not acquire a distant station.");
        PlaceWalker(world,ship,first.Id,ShipDeck.Station(true,1),now);
        PlaceWalker(world,ship,second.Id,new(.78,.35),now);
        var request=new DeckInputPacket {ShipId=world.State(ship).Id,Action=DeckAction.Interact};
        NavalSystem.DeckInput(first,request,now+10);NavalSystem.DeckInput(second,request,now+10);
        var state=(GunneryState)world.Invoke("GunnerState",first,now+10)!;
        Assert.That(state.LeaseId,Is.Not.EqualTo(Guid.Empty));Assert.That(state.Starboard,Is.True);Assert.That(state.Station,Is.EqualTo(1));
        Assert.That(((GunneryState)world.Invoke("GunnerState",second,now+10)!).LeaseId,Is.EqualTo(Guid.Empty));
        NavalSystem.DeckInput(first,new(){ShipId=request.ShipId,Side=-127},now+100);
        var held=Walkers(world,ship,now+100).Single(p=>p.PlayerId==first.Id);
        Assert.That(new DeckPoint(held.X,held.Y),Is.EqualTo(ShipDeck.Station(true,1)));
        NavalSystem.DeckInput(first,request,now+300);NavalSystem.DeckInput(second,request,now+310);
        Assert.That(((GunneryState)world.Invoke("GunnerState",second,now+310)!).LeaseId,Is.Not.EqualTo(Guid.Empty));
        var positions=Walkers(world,ship,now+310);var a=positions.Single(p=>p.PlayerId==first.Id);var b=positions.Single(p=>p.PlayerId==second.Id);
        Assert.That(ShipDeck.Distance(new(a.X,a.Y),new(b.X,b.Y)),Is.GreaterThanOrEqualTo(ShipDeck.Radius*2));
        NavalSystem.DeckInput(first,new(){ShipId=request.ShipId,Forward=127},now+450);
        var after=Walkers(world,ship,now+450).Single(p=>p.PlayerId==first.Id);
        Assert.That(ShipDeck.Distance(new(a.X,a.Y),new(after.X,after.Y)),Is.GreaterThan(.01),"The previous operator must be able to walk away after another player takes the cannon.");
    }
    [Test]
    public void BoatTranslationTurningAndMapChangesDoNotChangeRelativeDeckCoordinates()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var player=GunnerPlayer(world,ship);var now=Timing.Global.Milliseconds;
        PlaceWalker(world,ship,player.Id,new(.5,1),now);
        var state=world.State(ship);state.Speed=3;
        foreach(var heading in new[]{0,45,90,180,270,355})
        {
            state.X+=.25;state.Y+=.1;state.Heading=heading;
            var p=Walkers(world,ship,now).Single(p=>p.PlayerId==player.Id);
            Assert.That(new DeckPoint(p.X,p.Y),Is.EqualTo(new DeckPoint(.5,1)));
        }
        state.MapId=Guid.NewGuid();player.MapId=state.MapId;
        NavalSystem.DeckInput(player,new(){ShipId=state.Id,Forward=127},now+100);
        var moved=Walkers(world,ship,now+100).Single(p=>p.PlayerId==player.Id);
        Assert.That(moved.Y,Is.GreaterThan(1));Assert.That(moved.X,Is.EqualTo(.5));
    }
    [Test]
    public void TheCaptainCanLeaveTheHelmWalkAndReclaimItWithoutDisembarking()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);EnableWalking(world,ship);
        var captain=GunnerPlayer(world,ship);captain.Id=world.State(ship).CaptainId;var now=Timing.Global.Milliseconds;
        world.Invoke("EnsureDeck",ship,now);
        var request=new DeckInputPacket {ShipId=world.State(ship).Id,Action=DeckAction.Interact};
        NavalSystem.DeckInput(captain,request,now+10);
        Assert.That(world.Invoke("Driver",ship),Is.EqualTo(Guid.Empty));
        Assert.That(captain.NavalShipId,Is.EqualTo(request.ShipId));
        NavalSystem.DeckInput(captain,new(){ShipId=request.ShipId,Side=127},now+100);
        Assert.That(Walkers(world,ship,now+100).Single(p=>p.PlayerId==captain.Id).X,Is.GreaterThan(0));
        NavalSystem.DeckInput(captain,request,now+300);
        Assert.That(world.Invoke("Driver",ship),Is.EqualTo(captain.Id));
        Assert.That(world.Field<Guid>(ship,"HelmsmanId"),Is.EqualTo(Guid.Empty),"The captain uses the owner helm, not a crew delegation that the management tick would revoke.");
    }
}
