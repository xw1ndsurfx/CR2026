using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Server.Entities;
using Intersect.Server.Maps;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed class Cannonball
    {
        internal Guid Id = Guid.NewGuid(), ShipId, OwnerId, MapId, CaptainId;
        internal double X,Y,DirectionX,DirectionY,Remaining,Speed;
        internal int Damage;
        internal bool Enemy;
        internal double DockSafeRange;
        internal long Born;
        internal double Heading;
    }
    private static readonly List<Cannonball> Cannonballs = [];
    private static long _lastCombatStep;

    // Caller holds the owning player's entity lock, before Gate. Saves never acquire another captain's lock.
    private static bool ApplyPendingDamage(Player player,long now,bool force=false)
    {
        int damage;Guid id;
        lock(Gate)
        {
            if(!Ships.TryGetValue(player.CaptainedShipId,out var vessel) || vessel.OwnerId!=player.Id || vessel.PendingDamage<=0) return true;
            if(!force && now<vessel.NextDamageAttempt) return false;
            id=vessel.State.Id;damage=vessel.PendingDamage;vessel.PendingDamage=0;
        }
        if(DamageOwnedShip(player,id,damage)) return true;
        lock(Gate) if(Ships.TryGetValue(id,out var retry) && retry.SunkUntil==0)
        { retry.PendingDamage=Math.Min(100,retry.PendingDamage+damage);retry.NextDamageAttempt=now+1000; }
        return false;
    }

    internal static void Action(Player player, ShipActionPacket packet, long now)
    {
        lock (player.EntityLock)
        {
            var leave = false;
            lock (Gate)
            {
                if (!IsEnabled() || !player.InGame || player.IsDead || player.HasPendingDeath || player.NavalBusy || player.Z != 0 ||
                    player.InstanceType != MapInstanceType.Overworld || player.CaptainedShipId != packet.ShipId ||
                    !Ships.TryGetValue(packet.ShipId,out var vessel) || vessel.State.CaptainId != player.Id ||
                    vessel.State.MapId != player.MapId || vessel.SunkUntil > 0 ||
                    (packet.Action is ShipAction.Anchor or ShipAction.LeaveHelm && now-vessel.LastAction < 150) ||
                    !ShipActions.IsServerAction(packet.Action)) return;
                if (packet.Action is ShipAction.PortCannons or ShipAction.StarboardCannons &&
                    !ShipActions.CanFire(_settings.CannonsEnabled,true,false,now,
                        packet.Action==ShipAction.PortCannons?vessel.PortReady:vessel.StarboardReady)) return;
                vessel.LastAction = now;
                player.RegisterAfkActivity();
                if (packet.Action == ShipAction.LeaveHelm) leave = true;
                else if (packet.Action == ShipAction.Anchor)
                {vessel.Anchored=!vessel.Anchored;Emit(NavalEffectKind.Anchor,vessel.State.MapId,vessel.State.X,vessel.State.Y,0,now);}
                else Fire(vessel,packet.Action == ShipAction.StarboardCannons, now);
            }
            if (leave) Helm(player);
        }
    }

    private static void Fire(Vessel vessel, bool starboard, long now)
    {
        var ready = starboard ? vessel.StarboardReady : vessel.PortReady;
        if (!ShipActions.CanFire(_settings.CannonsEnabled,vessel.SunkUntil==0,false,now,ready) || Cannonballs.Count > 509) return;
        if (starboard) vessel.StarboardReady = now + _settings.CannonReloadMilliseconds;
        else vessel.PortReady = now + _settings.CannonReloadMilliseconds;
        var s=vessel.State; var d=vessel.Definition;
        var direction=ShipActions.Direction(s.Heading,starboard);
        var forwardX=Math.Sin(s.Heading*Math.PI/180); var forwardY=-Math.Cos(s.Heading*Math.PI/180);
        var radius=d.Width/2+.2;
        var ocean=OceanMapResolver.Maps();
        var sounded=false;
        for (var gun=-1;gun<=1;gun++)
        {
            var map=s.MapId; var x=s.X+direction.X*radius+forwardX*gun*d.Length/4;
            var y=s.Y+direction.Y*radius+forwardY*gun*d.Length/4;
            if (!OceanMapResolver.Resolve(ocean,ref map,ref x,ref y) || BlocksCannonball(map,x,y)) continue;
            Emit(NavalEffectKind.Fire,map,x,y,ShipMovement.Normalize(s.Heading+(starboard?90:-90)),now,!sounded);sounded=true;
            Cannonballs.Add(new() { CaptainId=s.CaptainId,Enemy=vessel.Enemy!=null,DockSafeRange=vessel.Enemy?.DockSafeRange??0,Born=now,Heading=ShipMovement.Normalize(s.Heading+(starboard?90:-90)),ShipId=s.Id,OwnerId=vessel.OwnerId,MapId=map,X=x,Y=y,
                DirectionX=direction.X,DirectionY=direction.Y,Remaining=_settings.CannonRange,Speed=_settings.CannonSpeed,Damage=vessel.Enemy?.CannonDamage??_settings.CannonDamage });
        }
    }

    private static bool BlocksCannonball(Guid mapId, double x, double y)
    {
        var map=MapController.Get(mapId); var tx=(int)Math.Floor(x);var ty=(int)Math.Floor(y);
        if (map?.Attributes == null || tx<0 || ty<0 || tx>=map.Attributes.GetLength(0) || ty>=map.Attributes.GetLength(1) ||
            IsBlockingAttribute(map.Attributes[tx,ty])) return true;
        if (!map.TryGetInstance(MapInstance.OverworldInstanceId,out var instance) || instance.IsDisposed) return true;
        return instance.GetCachedEntities().Any(e=>e.Z==0 && !e.IsPassable() && e.X==tx && e.Y==ty && e is not Player) ||
            instance.GlobalEventInstances.Values.SelectMany(e=>e.GlobalPageInstance).Any(e=>e!=null && !e.Passable && e.Z==0 && e.X==tx && e.Y==ty);
    }

    private static void UpdateCannonballs(long now)
    {
        if (!_settings.CannonsEnabled) { Cannonballs.Clear(); _lastCombatStep=now; return; }
        if (_lastCombatStep==0) { _lastCombatStep=now; return; }
        var seconds=(now-_lastCombatStep)/1000d;
        if (seconds<.025) return; // One shared combat step, independent of how many captains update this tick.
        _lastCombatStep=now;
        var ocean=OceanMapResolver.Maps();
        var hulls = new Dictionary<(Guid Map,int X,int Y),Vessel>();
        if (Cannonballs.Count>0)
            foreach(var vessel in Ships.Values.Where(v=>v.SunkUntil==0))
                foreach(var tile in HullTiles(vessel,ocean,vessel.State.MapId,vessel.State.X,vessel.State.Y)) hulls.TryAdd(tile,vessel);
        foreach (var ball in Cannonballs.ToArray())
        {
            if (!Ships.ContainsKey(ball.ShipId)) { Cannonballs.Remove(ball);continue; }
            var impact=NavalEffectKind.WaterImpact;
            var originMap=ball.MapId;var originX=ball.X;var originY=ball.Y;var stopped=false;
            var travelled=ShipCannonSweep.Travel(ball.Speed,seconds,ball.Remaining,distance=>
            {
                var map=originMap;var x=originX+ball.DirectionX*distance;var y=originY+ball.DirectionY*distance;
                if (!OceanMapResolver.Resolve(ocean,ref map,ref x,ref y) || BlocksCannonball(map,x,y))
                { impact=NavalEffectKind.ObstacleImpact;stopped=true;return false; }
                ball.MapId=map;ball.X=x;ball.Y=y;
                var tile=(map,(int)Math.Floor(x),(int)Math.Floor(y));
                if (!hulls.TryGetValue(tile,out var target) || target.State.Id==ball.ShipId || target.SunkUntil>0) return true;
                impact=NavalEffectKind.HullImpact;
                var permitted=ShipEnemySteering.CanDamage(ball.Enemy,target.Enemy!=null,target.OwnerId!=Guid.Empty || target.State.CaptainId!=Guid.Empty,
                    ball.OwnerId!=Guid.Empty && ball.OwnerId==target.OwnerId,_settings.PlayerShipDamage);
                if(ball.Enemy && (DockProtected(map,x,y,ball.DockSafeRange) ||
                    DockProtected(target.State.MapId,target.State.X,target.State.Y,ball.DockSafeRange))) permitted=false;
                if(permitted && Ships.TryGetValue(ball.ShipId,out var attacker)) Engage(attacker,target,now);
                // Protected player hulls still intercept shots; no firing through another ship.
                if (permitted && target.OwnerId==Guid.Empty)
                {
                    target.Health=Math.Max(0,target.Health-ball.Damage);
                    target.State.DamageState=target.Health<target.MaximumHealth?1:0;
                    if (target.Health==0)
                    {
                        CreditEnemySink(target,ball.ShipId,ball.CaptainId);
                        target.State.Speed=target.State.Throttle=0;target.Controls.Reset();
                        target.SunkUntil=now+Math.Max(2000,target.Definition.FrameMilliseconds*4);
                        target.SinkingGraphics=target.Definition.SinkingGraphics;
                        Emit(NavalEffectKind.Sinking,target.State.MapId,target.State.X,target.State.Y,target.State.Heading,now);
                    }
                }
                else if (permitted && target.OwnerId!=Guid.Empty)
                    target.PendingDamage=Math.Min(100,target.PendingDamage+ball.Damage);
                stopped=true;return false;
            });
            ball.Remaining-=travelled;
            if (stopped || ball.Remaining<=0) {Emit(impact,ball.MapId,ball.X,ball.Y,ball.Heading,now);Cannonballs.Remove(ball);}
        }
        // Unoccupied public test targets disappear after their sinking animation until server restart/configuration reload.
        foreach (var vessel in Ships.Values.Where(v=>v.Enemy==null && v.OwnerId==Guid.Empty && v.State.CaptainId==Guid.Empty && v.SunkUntil>0 && now>=v.SunkUntil).ToArray())
            Ships.Remove(vessel.State.Id);
    }
}
