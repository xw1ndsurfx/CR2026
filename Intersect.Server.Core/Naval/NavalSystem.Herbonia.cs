using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Maps;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private static long _lastEnemyStep;
    private static HashSet<(Guid Map,int X,int Y)> _enemyEventTiles=[];
    private static readonly Dictionary<Guid,(int X,int Y)[]> EnemyDocks=[];
    private static readonly Dictionary<Guid,HashSet<(Guid Map,int X,int Y)>> EnemyHulls=[];

    // Called by the world loop, once per 50 ms, independently of captain/player updates.
    // No player locks, database writes or native map creation occur while holding Gate.
    internal static void WorldTick(long now)
    {
        lock(Gate)
        {
            if(now-_lastEnemyStep<50) return;
            var dt=_lastEnemyStep==0?0:Math.Clamp((now-_lastEnemyStep)/1000d,0,.1);
            _lastEnemyStep=now;
            if(!IsEnabled()) return;
            var ocean=OceanMapResolver.Maps();
            EnemyDocks.Clear();
            EnemyHulls.Clear();
            foreach(var ship in Ships.Values.Where(v=>v.SunkUntil==0))
                EnemyHulls[ship.State.Id]=HullTiles(ship,ocean,ship.State.MapId,ship.State.X,ship.State.Y);
            _enemyEventTiles=EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e=>!e.CommonEvent && e.Pages.Any(p=>!p.Passable))
                .Select(e=>(e.MapId,e.SpawnX,e.SpawnY)).ToHashSet();
            foreach(var patrol in _settings.Herbonia.Where(p=>p.Enabled))
            {
                if(Ships.ContainsKey(patrol.Ship.Id) || !ocean.Contains(patrol.Ship.MapId)) continue;
                var spawned=new Vessel(patrol.Ship) { Enemy=patrol,Health=patrol.MaximumHealth };
                if(ClearEnemyHull(spawned,ocean,spawned.State.MapId,spawned.State.X,spawned.State.Y))
                {
                    Ships.Add(spawned.State.Id,spawned);
                    EnemyHulls[spawned.State.Id]=HullTiles(spawned,ocean,spawned.State.MapId,spawned.State.X,spawned.State.Y);
                }
            }
            foreach(var enemy in Ships.Values.Where(v=>v.Enemy!=null).ToArray())
            {
                StepEnemy(enemy,ocean,now,dt);
                var current=Ships[enemy.State.Id];
                if(current.SunkUntil==0) EnemyHulls[current.State.Id]=HullTiles(current,ocean,current.State.MapId,current.State.X,current.State.Y);
            }
            UpdateCannonballs(now);
        }
    }

    private static bool ClearEnemyHull(Vessel vessel,HashSet<Guid> ocean,Guid id,double x,double y)
    {
        var tiles=HullTiles(vessel,ocean,id,x,y);
        foreach(var tile in tiles)
        {
            var map=MapController.Get(tile.Map);
            if(map?.Attributes==null || tile.X<0 || tile.Y<0 || tile.X>=map.Attributes.GetLength(0) || tile.Y>=map.Attributes.GetLength(1) ||
                IsBlockingAttribute(map.Attributes[tile.X,tile.Y]) || !map.TryGetInstance(MapInstance.OverworldInstanceId,out var instance) || instance.IsDisposed ||
                instance.GetCachedEntities().Any(e=>!e.IsPassable() && e.Z==0 && e.X==tile.X && e.Y==tile.Y) ||
                instance.GlobalEventInstances.Values.SelectMany(e=>e.GlobalPageInstance).Any(e=>e!=null && !e.Passable && e.Z==0 && e.X==tile.X && e.Y==tile.Y) ||
                _enemyEventTiles.Contains(tile)) return false;
        }
        return !EnemyHulls.Any(other=>other.Key!=vessel.State.Id && tiles.Overlaps(other.Value));
    }

    // Coordinates derived only from reciprocal Ocean links. Bound the search to neighbouring maps.
    private static (double X,double Y)? Relative(Guid origin,Guid destination,double x,double y,HashSet<Guid> ocean)
    {
        var queue=new Queue<(Guid Id,int X,int Y)>();var visited=new HashSet<Guid>();queue.Enqueue((origin,0,0));
        while(queue.Count>0 && visited.Count<25)
        {
            var current=queue.Dequeue();
            if(!visited.Add(current.Id) || !ocean.Contains(current.Id)) continue;
            if(current.Id==destination) return (x+current.X*Options.Instance.Map.MapWidth,y+current.Y*Options.Instance.Map.MapHeight);
            if(Math.Abs(current.X)>=2 || Math.Abs(current.Y)>=2 || MapController.Get(current.Id) is not {} map) continue;
            if(MapController.Get(map.Left)?.Right==current.Id) queue.Enqueue((map.Left,current.X-1,current.Y));
            if(MapController.Get(map.Right)?.Left==current.Id) queue.Enqueue((map.Right,current.X+1,current.Y));
            if(MapController.Get(map.Up)?.Down==current.Id) queue.Enqueue((map.Up,current.X,current.Y-1));
            if(MapController.Get(map.Down)?.Up==current.Id) queue.Enqueue((map.Down,current.X,current.Y+1));
        }
        return null;
    }

    private static bool DockProtected(Guid map,double x,double y,double range)
    {
        if(!EnemyDocks.TryGetValue(map,out var docks))
            EnemyDocks[map]=docks=ShipDock.Tiles(MapController.Get(map)?.Attributes).ToArray();
        return docks.Any(q=>(q.X+.5-x)*(q.X+.5-x)+(q.Y+.5-y)*(q.Y+.5-y)<=range*range);
    }
    private static bool SafeDock(Vessel enemy,Guid map,double x,double y) => DockProtected(map,x,y,enemy.Enemy.DockSafeRange);

    private static void StepEnemy(Vessel enemy,HashSet<Guid> ocean,long now,double dt)
    {
        var config=enemy.Enemy;var s=enemy.State;var d=enemy.Definition;
        if(enemy.SunkUntil>0)
        {
            if(enemy.RespawnAt==0) enemy.RespawnAt=enemy.SunkUntil+config.RespawnSeconds*1000L;
            if(now<enemy.RespawnAt) return;
            var fresh=new Vessel(d) {Enemy=config,Health=config.MaximumHealth};
            if(ClearEnemyHull(fresh,ocean,d.MapId,d.X,d.Y)) Ships[s.Id]=fresh;
            return;
        }
        if(!ocean.Contains(s.MapId) || MapController.Get(s.MapId) is not {} currentMap || !currentMap.TryGetInstance(MapInstance.OverworldInstanceId,out var instance) || instance.IsDisposed) return;
        var home=Relative(s.MapId,d.MapId,d.X,d.Y,ocean);
        if(home==null) { s.Speed=s.Throttle=0;return; } // Edited topology: never teleport to home.
        var homeDistance=Distance(s.X,s.Y,home.Value.X,home.Value.Y);
        Vessel target=null; (double X,double Y) point=home.Value;
        var nearest=config.DetectionRange;
        if(homeDistance<=config.LeashRadius && !SafeDock(enemy,s.MapId,s.X,s.Y))
            foreach(var candidate in Ships.Values.Where(v=>v.Enemy==null && v.State.CaptainId!=Guid.Empty && v.SunkUntil==0))
            {
                var other=Relative(s.MapId,candidate.State.MapId,candidate.State.X,candidate.State.Y,ocean);
                var fromHome=Relative(d.MapId,candidate.State.MapId,candidate.State.X,candidate.State.Y,ocean);
                if(other==null || fromHome==null || Distance(d.X,d.Y,fromHome.Value.X,fromHome.Value.Y)>config.LeashRadius ||
                    SafeDock(enemy,candidate.State.MapId,candidate.State.X,candidate.State.Y)) continue;
                var distance=Distance(s.X,s.Y,other.Value.X,other.Value.Y);
                if(distance<nearest) { target=candidate;point=other.Value;nearest=distance; }
            }
        double desired;
        if(target!=null)
        {
            var bearing=ShipEnemySteering.Bearing(point.X-s.X,point.Y-s.Y);
            if(nearest>_settings.CannonRange*.7) desired=bearing;
            else
            {
                // Choose the closer broadside heading, then orbit instead of ramming the target.
                var port=ShipMovement.Normalize(bearing+90);var starboard=ShipMovement.Normalize(bearing-90);
                desired=Math.Abs(ShipEnemySteering.Error(s.Heading,port))<Math.Abs(ShipEnemySteering.Error(s.Heading,starboard))?port:starboard;
                if(nearest<Math.Max(d.Length,4)) desired=ShipMovement.Normalize(bearing+180);
            }
            if(nearest<=_settings.CannonRange && ShipEnemySteering.BroadsideAligned(s.Heading,bearing) && EnemyLineClear(enemy,target,point,ocean))
                Fire(enemy,ShipEnemySteering.Error(s.Heading,bearing)>0,now);
        }
        else
        {
            if(homeDistance<config.LeashRadius && s.MapId==d.MapId)
            {
                var angle=enemy.Waypoint*Math.PI/2;
                point=(home.Value.X+Math.Sin(angle)*config.PatrolRadius,home.Value.Y-Math.Cos(angle)*config.PatrolRadius);
                if(Distance(s.X,s.Y,point.X,point.Y)<1) enemy.Waypoint=(enemy.Waypoint+1)%4;
            }
            desired=ShipEnemySteering.Bearing(point.X-s.X,point.Y-s.Y);
        }
        var input=target==null && config.PatrolRadius==0 && homeDistance<1 ? new ShipInput(0,0,true) : ShipEnemySteering.Towards(s.Heading,desired);
        var lookahead=Math.Max(1,s.Speed*s.Speed/(2*d.Acceleration)+.5);
        var nextMap=s.MapId;var nx=s.X+Math.Sin(s.Heading*Math.PI/180)*lookahead;var ny=s.Y-Math.Cos(s.Heading*Math.PI/180)*lookahead;
        if(!OceanMapResolver.Resolve(ocean,ref nextMap,ref nx,ref ny) || !ClearEnemyHull(enemy,ocean,nextMap,nx,ny)) enemy.AvoidUntil=now+1200;
        if(now<enemy.AvoidUntil) input=new(0,1,false);
        ShipMovement.Step(s,d,input,dt,(map,x,y,heading)=>
        {
            var clear=OceanMapResolver.Resolve(ocean,ref map,ref x,ref y) && ClearEnemyHull(enemy,ocean,map,x,y);
            return (clear,map,x,y);
        });
    }

    private static bool EnemyLineClear(Vessel enemy,Vessel target,(double X,double Y) point,HashSet<Guid> ocean)
    {
        var s=enemy.State;var length=Distance(s.X,s.Y,point.X,point.Y);var steps=(int)Math.Ceiling(length*4);
        for(var step=1;step<=steps;step++)
        {
            var map=s.MapId;var x=s.X+(point.X-s.X)*step/steps;var y=s.Y+(point.Y-s.Y)*step/steps;
            if(!OceanMapResolver.Resolve(ocean,ref map,ref x,ref y) || BlocksCannonball(map,x,y) || SafeDock(enemy,map,x,y)) return false;
        }
        return true;
    }
}
