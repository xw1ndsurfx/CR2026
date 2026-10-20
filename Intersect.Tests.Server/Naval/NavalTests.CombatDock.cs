using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void QIsASolidMapBlockForOrdinaryEntitiesAndIsNeverALandingTile()
    {
        var entity=(Entity)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(entity);
        var method=typeof(Entity).GetMethod("IsBlockedByMapAttribute",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var args=new object?[]{Direction.DownRight,new MapDockAttribute(),null};
        Assert.That(method.Invoke(entity,args),Is.True);
        var tiles=new MapAttribute[8,8];tiles[4,4]=new MapDockAttribute();tiles[4,3]=new MapBlockedAttribute();
        var landing=ShipDock.FindLanding(tiles,4.5,6.5,5,(x,y)=>!NavalBlock(tiles[x,y]));
        Assert.That(landing,Is.EqualTo((4,5)));
        Assert.That(tiles[landing!.Value.X,landing.Value.Y],Is.Not.TypeOf<MapDockAttribute>());
        Assert.That(ShipDock.FindLanding(tiles,4.5,6.5,5,(_,_)=>false),Is.Null);
        Assert.That(ShipDock.FindLanding(tiles,double.NaN,5,5,(_,_)=>true),Is.Null);
    }
    private static bool NavalBlock(MapAttribute attribute)=>Intersect.Server.Naval.NavalSystem.IsBlockingAttribute(attribute);

    [Test]
    public void QLandingOnlyUsesAnAdjacentTileAndStaysInsideMapAndReach()
    {
        var tiles=new MapAttribute[8,8];tiles[0,0]=new MapDockAttribute();tiles[1,0]=new MapDockAttribute();
        Assert.That(ShipDock.FindLanding(tiles,.5,.5,1.5,(x,y)=>x==0&&y==1),Is.EqualTo((0,1)));
        Assert.That(ShipDock.FindLanding(tiles,.5,.5,1.5,(x,y)=>x==1&&y==1),Is.EqualTo((1,1)));
        Assert.That(ShipDock.FindLanding(tiles,7.5,7.5,2,(_,_)=>true),Is.Null);
        Assert.That(ShipDock.FindLanding(tiles,.5,.5,1.5,(x,y)=>x==2&&y==2),Is.Null);
        Assert.That(ShipDock.FindLanding(null,0,0,5,(_,_)=>true),Is.Null);
    }

    [TestCase(0,false,-1,0)]
    [TestCase(0,true,1,0)]
    [TestCase(90,false,0,-1)]
    [TestCase(90,true,0,1)]
    [TestCase(180,false,1,0)]
    [TestCase(180,true,-1,0)]
    [TestCase(270,false,0,1)]
    [TestCase(270,true,0,-1)]
    public void BroadsideDirectionsAreRelativeToTheHull(double heading,bool starboard,double x,double y)
    {
        var direction=ShipActions.Direction(heading,starboard);
        Assert.That(direction.X,Is.EqualTo(x).Within(.0001));Assert.That(direction.Y,Is.EqualTo(y).Within(.0001));
    }

    [Test]
    public void ASlowTickCannotShootThroughAnObstacleOrSpendMoreThanRemainingRange()
    {
        var samples=new List<double>();
        var travelled=ShipCannonSweep.Travel(30,5,40,x=>{samples.Add(x);return x<.75;});
        Assert.That(travelled,Is.LessThanOrEqualTo(.85));
        Assert.That(samples.Zip(samples.Skip(1),(a,b)=>b-a),Is.All.LessThanOrEqualTo(.10001));
        Assert.That(ShipCannonSweep.Travel(30,5,.05,_=>true),Is.EqualTo(.05));
        Assert.That(ShipCannonSweep.Travel(12,double.NaN,20,_=>throw new Exception()),Is.Zero);
        Assert.That(ShipCannonSweep.Travel(12,-1,20,_=>throw new Exception()),Is.Zero);
    }

    [Test]
    public void LocalCannonPreviewAndForgedActionsCannotFallThroughToServerFire()
    {
        foreach(var action in new[]{ShipAction.CannonView,ShipAction.Crew,ShipAction.Cargo,ShipAction.None,(ShipAction)255})
            Assert.That(ShipActions.IsServerAction(action),Is.False);
        foreach(var action in new[]{ShipAction.PortCannons,ShipAction.StarboardCannons,ShipAction.Anchor,ShipAction.LeaveHelm})
            Assert.That(ShipActions.IsServerAction(action),Is.True);
        Assert.That((byte)ShipAction.Crew,Is.EqualTo(6));Assert.That((byte)ShipAction.CannonView,Is.EqualTo(7));
    }

    [Test]
    public void ReloadAndEligibilityAreIndependentPerBroadside()
    {
        Assert.That(ShipActions.CanFire(true,true,false,1000,5000),Is.False);
        Assert.That(ShipActions.CanFire(true,true,false,1000,0),Is.True);
        Assert.That(ShipActions.CanFire(false,true,false,6000,0),Is.False);
        Assert.That(ShipActions.CanFire(true,false,false,6000,0),Is.False);
        Assert.That(ShipActions.CanFire(true,true,true,6000,0),Is.False);
        Assert.That(ShipActions.CanFire(true,true,false,5000,5000),Is.True);
    }

    [Test]
    public void ActionAndCombatSnapshotsRoundTripAndTheClientCannotSupplyDamageOrPositions()
    {
        var request=new ShipActionPacket {ShipId=Guid.NewGuid(),Action=ShipAction.PortCannons};
        var roundtrip=MessagePackSerializer.Deserialize<ShipActionPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(roundtrip.ShipId,Is.EqualTo(request.ShipId));Assert.That(roundtrip.Action,Is.EqualTo(ShipAction.PortCannons));
        Assert.That(typeof(ShipActionPacket).GetProperties().Where(p=>p.GetCustomAttribute<KeyAttribute>()!=null).Select(p=>p.Name),
            Is.EquivalentTo(new[]{"ShipId","Action"}));
        var snapshot=new NavalSnapshotPacket { Ships=[new(){Id=request.ShipId,Health=35,PortReloadMilliseconds=3500,StarboardReloadMilliseconds=0,
            CannonsEnabled=true,Anchored=true}],Cannonballs=[new(){Id=Guid.NewGuid(),MapId=Map,X=2.25,Y=7.75}] };
        var restored=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(snapshot));
        Assert.That(restored.Ships[0].Health,Is.EqualTo(35));Assert.That(restored.Ships[0].Anchored,Is.True);
        Assert.That(restored.Ships[0].PortReloadMilliseconds,Is.EqualTo(3500));Assert.That(restored.Cannonballs[0].X,Is.EqualTo(2.25));
    }

    [TestCase("CannonDamage",0)]
    [TestCase("CannonDamage",101)]
    [TestCase("CannonReloadMilliseconds",999)]
    [TestCase("CannonReloadMilliseconds",60001)]
    [TestCase("CannonRange",41)]
    [TestCase("CannonSpeed",0)]
    public void SystemEditorRejectsUnboundedCannonSettings(string property,int value)
    {
        var settings=new NavalConfiguration();var prop=typeof(NavalConfiguration).GetProperty(property)!;
        if(prop.PropertyType==typeof(double)) prop.SetValue(settings,(double)value);else prop.SetValue(settings,value);
        Assert.Throws<InvalidDataException>(()=>settings.Validate(32,26));
    }

    [TestCase(false,false,false,false,70,0,false)]
    [TestCase(true,false,false,false,100,0,false)]
    [TestCase(false,true,false,false,100,0,false)]
    [TestCase(false,false,true,false,100,0,false)]
    [TestCase(false,false,true,true,100,30,false)]
    [TestCase(false,false,false,false,70,0,true)]
    public void ServerBroadsideTravelsHitsOrStopsAndRespectsPlayerProtection(bool wall,bool shortRange,bool owned,bool pvp,int health,int pending,bool crossing)
    {
        // In-memory maps/instances only. No server services, databases, users or network connections are started.
        Options.EnsureCreated();
        var type=typeof(Intersect.Server.Naval.NavalSystem);const BindingFlags stat=BindingFlags.Static|BindingFlags.NonPublic;
        const BindingFlags inst=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var ships=(System.Collections.IDictionary)type.GetField("Ships",stat)!.GetValue(null)!;
        var balls=(System.Collections.IList)type.GetField("Cannonballs",stat)!.GetValue(null)!;
        var savedShips=new List<System.Collections.DictionaryEntry>();foreach(System.Collections.DictionaryEntry item in ships) savedShips.Add(item);
        var savedBalls=balls.Cast<object>().ToArray();
        var settingsField=type.GetField("_settings",stat)!;var savedSettings=settingsField.GetValue(null);
        var timeField=type.GetField("_lastCombatStep",stat)!;var savedTime=timeField.GetValue(null);
        var savedList=Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List;
        var map=new Intersect.Server.Maps.MapController(Guid.NewGuid());
        Intersect.Server.Maps.MapController.Lookup.Set(map.Id,map);
        var adjacent=new Intersect.Server.Maps.MapController(Guid.NewGuid());
        Intersect.Server.Maps.MapController.Lookup.Set(adjacent.Id,adjacent);
        map.Right=adjacent.Id;adjacent.Left=map.Id;
        try
        {
            ships.Clear();balls.Clear();timeField.SetValue(null,0L);
            settingsField.SetValue(null,new NavalConfiguration {Enabled=true,CannonsEnabled=true,PlayerShipDamage=pvp,CannonRange=shortRange?1:12});
            Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List=new() { Items=[new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListFolder {
                Name="Ocean",Children=new() {Items=[new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListMap {MapId=map.Id},new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListMap {MapId=adjacent.Id}]} }] };
            foreach(var currentMap in new[]{map,adjacent})
            {
            var instance=(Intersect.Server.Maps.MapInstance)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Server.Maps.MapInstance));GC.SuppressFinalize(instance);
            typeof(Intersect.Server.Maps.MapInstance).GetField("mCachedEntities",inst)!.SetValue(instance,Array.Empty<Entity>());
            var events=typeof(Intersect.Server.Maps.MapInstance).GetField("GlobalEventInstances",inst)!;
            events.SetValue(instance,Activator.CreateInstance(events.FieldType));
            ((System.Collections.IDictionary)typeof(Intersect.Server.Maps.MapController).GetField("mInstances",inst)!.GetValue(currentMap)!)[Guid.Empty]=instance;
            }
            if(wall) for(var y=0;y<Options.Instance.Map.MapHeight;y++) map.Attributes[10,y]=new MapDockAttribute();
            var vesselType=type.GetNestedType("Vessel",BindingFlags.NonPublic)!;
            object Vessel(Guid mapId,double x)=>Activator.CreateInstance(vesselType,inst,null,new object[]{new ShipDefinition {MapId=mapId,X=x,Y=10.5}},null)!;
            var shooter=Vessel(map.Id,crossing?Options.Instance.Map.MapWidth-4.5:5.5);var target=Vessel(crossing?adjacent.Id:map.Id,crossing?4.5:14.5);
            var shooterState=(ShipState)vesselType.GetField("State",inst)!.GetValue(shooter)!;
            var targetState=(ShipState)vesselType.GetField("State",inst)!.GetValue(target)!;
            if(owned) vesselType.GetField("OwnerId",inst)!.SetValue(target,Guid.NewGuid());
            ships.Add(shooterState.Id,shooter);ships.Add(targetState.Id,target);
            type.GetMethod("Fire",stat)!.Invoke(null,[shooter,true,2000L]);
            Assert.That(balls.Count,Is.EqualTo(3));
            Assert.That(vesselType.GetField("StarboardReady",inst)!.GetValue(shooter),Is.EqualTo(6000L));
            Assert.That(vesselType.GetField("PortReady",inst)!.GetValue(shooter),Is.EqualTo(0L));
            // Duplicate shots cannot bypass the server cooldown, while the opposite broadside can still fire.
            type.GetMethod("Fire",stat)!.Invoke(null,[shooter,true,2050L]);Assert.That(balls.Count,Is.EqualTo(3));
            for(var now=2000L;now<=4000;now+=50) type.GetMethod("UpdateCannonballs",stat)!.Invoke(null,[now]);
            Assert.That(vesselType.GetField("Health",inst)!.GetValue(target),Is.EqualTo(health));
            Assert.That(vesselType.GetField("PendingDamage",inst)!.GetValue(target),Is.EqualTo(pending));
            Assert.That(balls.Count,Is.Zero);
        }
        finally
        {
            ships.Clear();foreach(var item in savedShips) ships.Add(item.Key,item.Value);
            balls.Clear();foreach(var item in savedBalls) balls.Add(item);
            settingsField.SetValue(null,savedSettings);timeField.SetValue(null,savedTime);
            Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List=savedList;
            Intersect.Server.Maps.MapController.Lookup.Delete(map);
            Intersect.Server.Maps.MapController.Lookup.Delete(adjacent);
        }
    }
}
