using System.Reflection;
using Intersect.Framework.Core;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void BoardingCoordinatesPersistAndOnlyAFreeInBoundsTileIsSelected()
    {
        var old=JsonConvert.DeserializeObject<OwnedShip>("{\"ReturnDockX\":4,\"ReturnDockY\":5}")!;
        Assert.That(ShipReturnPoint.Exact(old,32,26,(_,_)=>true),Is.Null);
        var ship=JsonConvert.DeserializeObject<OwnedShip>(JsonConvert.SerializeObject(new OwnedShip {ReturnMapId=Map,ReturnX=7,ReturnY=8}))!;
        Assert.That(ShipReturnPoint.Exact(ship,32,26,(_,_)=>true),Is.EqualTo((7,8)));
        Assert.That(ShipReturnPoint.Exact(ship,32,26,(_,_)=>false),Is.Null);
        ship.ReturnX=33;Assert.That(ShipReturnPoint.Exact(ship,32,26,(_,_)=>true),Is.Null);
    }

    [TestCase(true,false,true,false,false,true)]
    [TestCase(false,true,false,false,false,true)]
    [TestCase(true,true,false,false,false,false)]
    [TestCase(false,false,true,false,false,false)]
    [TestCase(false,false,true,false,true,true)]
    [TestCase(false,false,true,true,true,false)]
    public void EnemyCombatDoesNotRequireOrEnablePlayerPvp(bool shooterEnemy,bool targetEnemy,bool targetPlayer,bool same,bool pvp,bool allowed) =>
        Assert.That(ShipEnemySteering.CanDamage(shooterEnemy,targetEnemy,targetPlayer,same,pvp),Is.EqualTo(allowed));

    [Test]
    public void EnemySettingsAreBoundedAndIdsCannotCollideWithPublicShips()
    {
        var definition=Definition();
        var config=new NavalConfiguration {Herbonia=[new(){Ship=definition}]};config.Validate(32,26);
        config.Herbonia[0].RespawnSeconds=0;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
        config.Herbonia[0].RespawnSeconds=60;config.Herbonia[0].DetectionRange=double.NaN;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
        config.Herbonia[0].DetectionRange=12;config.Ships=[definition];Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
        config.Ships=[null!];Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
    }

    [TestCase(false,false,false,70)]
    [TestCase(true,false,false,100)]
    [TestCase(false,true,false,100)]
    [TestCase(false,false,true,100)]
    public void EnemyWorldTickFiresWithoutPvpButHonoursShoreWallsAndCaptainPresence(bool dock,bool wall,bool noCaptain,int health)
    {
        using var world=new EnemyTestWorld();
        var enemy=world.Add(8.5,10.5,true);var target=world.Add(18.5,10.5,false);
        if(noCaptain) world.State(target).CaptainId=Guid.Empty;
        if(dock) world.Map.Attributes[19,10]=new MapDockAttribute();
        if(wall) for(var y=0;y<26;y++) world.Map.Attributes[13,y]=new MapBlockedAttribute();
        // World loop alone drives the enemy and its projectiles, no player Update call.
        for(var time=1000L;time<=3000;time+=50) NavalSystem.WorldTick(time);
        Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(health));
        var state=world.State(enemy);
        Assert.That(state.X,Is.InRange(0,32));Assert.That(state.Y,Is.InRange(0,26));
        if(wall) Assert.That(state.X,Is.LessThan(13));
    }

    [Test]
    public void EnemySinkingWaitsForRespawnAndFreeHomeWithoutTeleportingLiveShips()
    {
        using var world=new EnemyTestWorld();var enemy=world.Add(8.5,10.5,true);
        world.Set(enemy,"SunkUntil",2000L);world.Set(enemy,"Health",0);
        NavalSystem.WorldTick(2000);NavalSystem.WorldTick(12000);
        Assert.That(world.Field<int>(world.Ships[world.State(enemy).Id]!,"Health"),Is.Zero);
        world.Map.Attributes[8,10]=new MapBlockedAttribute();NavalSystem.WorldTick(63000);
        Assert.That(world.Field<int>(world.Ships[world.State(enemy).Id]!,"Health"),Is.Zero);
        world.Map.Attributes[8,10]=null!;NavalSystem.WorldTick(63100);
        var restored=world.Ships[world.State(enemy).Id]!;
        Assert.That(world.Field<int>(restored,"Health"),Is.EqualTo(100));Assert.That(world.State(restored).X,Is.EqualTo(8.5));
    }

    [Test]
    public void EnemySpawnsFromConfigurationOnceAndRejectsBlockedOrUnloadedHomes()
    {
        using var world=new EnemyTestWorld();
        var config=new NavalConfiguration {Enabled=true,Herbonia=[new() {Enabled=true,Ship=new() {MapId=world.Map.Id,X=10.5,Y=10.5}}]};
        world.Configure(config);
        world.Map.Attributes[10,10]=new MapBlockedAttribute();NavalSystem.WorldTick(1000);Assert.That(world.Ships.Count,Is.Zero);
        world.Map.Attributes[10,10]=null!;NavalSystem.WorldTick(1100);Assert.That(world.Ships.Count,Is.EqualTo(1));
        var state=world.State(world.Ships[config.Herbonia[0].Ship.Id]!);
        var x=state.X;var y=state.Y;NavalSystem.WorldTick(1100);
        Assert.That(world.Ships.Count,Is.EqualTo(1));Assert.That((state.X,state.Y),Is.EqualTo((x,y)));
        Assert.That(state.CaptainId,Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void EnemyPursuesThenReturnsWhenTargetLeavesItsHomeLeash()
    {
        using var world=new EnemyTestWorld();var enemy=world.Add(8.5,15.5,true);var target=world.Add(19.5,15.5,false);
        world.State(enemy).Heading=90;
        for(var time=1000L;time<=2000;time+=50) NavalSystem.WorldTick(time);
        Assert.That(world.State(enemy).X,Is.GreaterThan(8.5));
        world.State(target).X=31;world.State(target).Y=1;
        var before=world.State(enemy).Heading;NavalSystem.WorldTick(2100);
        Assert.That(Math.Abs(ShipEnemySteering.Error(before,world.State(enemy).Heading)),Is.LessThanOrEqualTo(4.50001));
        Assert.That(world.State(enemy).MapId,Is.EqualTo(world.Map.Id));
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");patrol.LeashRadius=12;
        world.State(enemy).X=25;world.State(enemy).Y=15.5;
        for(var time=2150L;time<=22150;time+=50) NavalSystem.WorldTick(time);
        Assert.That(Math.Abs(world.State(enemy).X-8.5),Is.LessThan(16.5),"A living enemy must sail back, rather than teleport or keep pursuing beyond its leash.");
    }

    [Test]
    public void EnemyTargetCoordinatesUseOnlyReciprocalOceanBorders()
    {
        using var world=new EnemyTestWorld();var other=new MapController(Guid.NewGuid());MapController.Lookup.Set(other.Id,other);
        try
        {
            world.Map.Right=other.Id;other.Left=world.Map.Id;
            var folder=(Intersect.Framework.Core.GameObjects.Maps.MapList.MapListFolder)Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List.Items[0];
            folder.Children.Items.Add(new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListMap {MapId=other.Id});
            var relative=typeof(NavalSystem).GetMethod("Relative",BindingFlags.Static|BindingFlags.NonPublic)!;
            var ocean=OceanMapResolver.Maps();
            var point=((double X,double Y)?)relative.Invoke(null,[world.Map.Id,other.Id,1d,10d,ocean]);
            Assert.That(point,Is.EqualTo(((double)Options.Instance.Map.MapWidth+1,10d)));
            other.Left=Guid.Empty;Assert.That(relative.Invoke(null,[world.Map.Id,other.Id,1d,10d,ocean]),Is.Null);
        }
        finally {MapController.Lookup.Delete(other);}
    }

    private sealed class EnemyTestWorld : IDisposable
    {
        private const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static;
        private const BindingFlags Instance=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
        private readonly Type type=typeof(NavalSystem),vessel=typeof(NavalSystem).GetNestedType("Vessel",BindingFlags.NonPublic)!;
        private readonly Dictionary<string,object?> saved=[];
        private readonly object[] balls,effects;
        private readonly System.Collections.DictionaryEntry[] ships;
        private readonly Intersect.Framework.Core.GameObjects.Maps.MapList.MapList mapList;
        internal readonly MapController Map;
        internal System.Collections.IDictionary Ships => (System.Collections.IDictionary)type.GetField("Ships",Static)!.GetValue(null)!;
        internal EnemyTestWorld()
        {
            Options.EnsureCreated();
            foreach(var name in new[]{"_settings","_loaded","_enabled","_lastEnemyStep","_lastCombatStep","_enemyEventTiles"}) saved[name]=type.GetField(name,Static)!.GetValue(null);
            ships=Ships.Keys.Cast<object>().Select(key=>new System.Collections.DictionaryEntry(key,Ships[key])).ToArray();Ships.Clear();
            balls=List("Cannonballs").Cast<object>().ToArray();effects=List("Effects").Cast<object>().ToArray();List("Cannonballs").Clear();List("Effects").Clear();
            type.GetField("_settings",Static)!.SetValue(null,new NavalConfiguration {Enabled=true,CannonsEnabled=true,PlayerShipDamage=false});
            type.GetField("_loaded",Static)!.SetValue(null,true);type.GetField("_enabled",Static)!.SetValue(null,true);
            type.GetField("_lastEnemyStep",Static)!.SetValue(null,0L);type.GetField("_lastCombatStep",Static)!.SetValue(null,0L);
            mapList=Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List;
            Map=new(Guid.NewGuid());MapController.Lookup.Set(Map.Id,Map);
            Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List=new(){Items=[new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListFolder {
                Name="Ocean",Children=new(){Items=[new Intersect.Framework.Core.GameObjects.Maps.MapList.MapListMap {MapId=Map.Id}]} }]};
            var instance=(MapInstance)RuntimeHelpers.GetUninitializedObject(typeof(MapInstance));GC.SuppressFinalize(instance);
            typeof(MapInstance).GetField("mCachedEntities",Instance)!.SetValue(instance,Array.Empty<Entity>());
            var events=typeof(MapInstance).GetField("GlobalEventInstances",Instance)!;events.SetValue(instance,Activator.CreateInstance(events.FieldType));
            ((System.Collections.IDictionary)typeof(MapController).GetField("mInstances",Instance)!.GetValue(Map)!)[Guid.Empty]=instance;
        }
        private System.Collections.IList List(string name)=>(System.Collections.IList)type.GetField(name,Static)!.GetValue(null)!;
        internal void Configure(NavalConfiguration config)=>type.GetField("_settings",Static)!.SetValue(null,config);
        internal object Add(double x,double y,bool enemy)
        {
            var d=new ShipDefinition {MapId=Map.Id,X=x,Y=y};
            var item=Activator.CreateInstance(vessel,Instance,null,[d],null)!;
            if(enemy) Set(item,"Enemy",new HerboniaPatrol {Enabled=true,Ship=d});
            else State(item).CaptainId=Guid.NewGuid();
            Ships.Add(State(item).Id,item);return item;
        }
        internal void Set(object item,string name,object value)=>vessel.GetField(name,Instance)!.SetValue(item,value);
        internal T Field<T>(object item,string name)=>(T)vessel.GetField(name,Instance)!.GetValue(item)!;
        internal ShipState State(object item)=>Field<ShipState>(item,"State");
        public void Dispose()
        {
            Ships.Clear();foreach(var entry in ships) Ships.Add(entry.Key,entry.Value);
            List("Cannonballs").Clear();foreach(var ball in balls) List("Cannonballs").Add(ball);
            List("Effects").Clear();foreach(var effect in effects) List("Effects").Add(effect);
            foreach(var entry in saved) type.GetField(entry.Key,Static)!.SetValue(null,entry.Value);
            Intersect.Framework.Core.GameObjects.Maps.MapList.MapList.List=mapList;MapController.Lookup.Delete(Map);
        }
    }
}
