using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database.PlayerData;
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
        internal CannonAmmunition Ammo;
        internal CannonFlight Flight;
        internal double Travelled;
        internal double Height=>Flight?.HeightAtDistance(Travelled)??0;
        internal Guid GunnerId;
        internal Guid ImpactAnimationId;
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
        lock(player.EntityLock)
        {
            var leave=false;var shots=new List<Cannonball>();CannonAmmunition ammo=null;Vessel reserved=null;long ready=0;var starboard=packet.Action==ShipAction.StarboardCannons;
            lock(Gate)
            {
                if(!IsEnabled() || player.NavalPartyExitRequested || !player.InGame || player.IsDead || player.HasPendingDeath || player.NavalBusy || player.Z!=0 ||
                    player.InstanceType!=MapInstanceType.Overworld || player.NavalShipId!=packet.ShipId ||
                    !Ships.TryGetValue(packet.ShipId,out var v) || Driver(v)!=player.Id || player.Id!=v.State.CaptainId && !player.Party.Any(m=>m.Id==v.State.CaptainId) || v.State.MapId!=player.MapId || v.SunkUntil>0 || !ShipActions.IsServerAction(packet.Action))return;
                if(packet.Action==ShipAction.LeaveHelm){if(v.State.CaptainId!=player.Id){v.HelmsmanId=Guid.Empty;v.Controls.Reset();return;}leave=true;}
                else if(packet.Action==ShipAction.Anchor)
                {if(now-v.LastAction<150)return;v.LastAction=now;v.Anchored=!v.Anchored;Emit(NavalEffectKind.Anchor,v.State.MapId,v.State.X,v.State.Y,0,now);}
                else
                {
                    ready=starboard?v.StarboardReady:v.PortReady;
                    if(!ShipActions.CanFire(_settings.CannonsEnabled,true,false,now,ready) || Cannonballs.Count+PendingCrewShots>509)return;
                    ammo=_settings.Ammunition.FirstOrDefault(a=>a.Kind==packet.Ammunition);
                    if(ammo==null || _settings.Sailing.Progress(player.SailingExperience).Level<ammo.RequiredSailingLevel ||
                        ammo.RequiresItem && (ItemDescriptor.Get(ammo.ItemId)?.IsStackable!=true || AmmoCount(player,ammo.ItemId)<ammo.UnitsPerShot*3))return;
                    reserved=v;FireAmmunition(v,starboard,now,ammo,shots);PendingCrewShots+=shots.Count;
                }
            }
            if(leave){Helm(player);return;}
            if(reserved==null)return;
            var paid=false;
            try {paid=shots.Count>0 && (!ammo.RequiresItem || CommitAmmo(player,ammo.ItemId,-ammo.UnitsPerShot*shots.Count,()=>player.User?.Save(force:true)==UserSaveResult.Completed));}
            finally
            {
                lock(Gate)
                {
                    PendingCrewShots-=shots.Count;
                    if(paid){Cannonballs.AddRange(shots);foreach(var shot in shots)Emit(NavalEffectKind.Fire,shot.MapId,shot.X,shot.Y,shot.Heading,now);player.RegisterAfkActivity();}
                    else if(starboard)reserved.StarboardReady=ready;else reserved.PortReady=ready;
                    SendSnapshot(player);
                }
            }
        }
    }

    private static void Fire(Vessel vessel,bool starboard,long now)=>FireAmmunition(vessel,starboard,now,null,null);
    private static void FireAmmunition(Vessel vessel, bool starboard, long now,CannonAmmunition ammo,List<Cannonball> planned)
    {
        var ready = starboard ? vessel.StarboardReady : vessel.PortReady;
        if (!ShipActions.CanFire(_settings.CannonsEnabled,vessel.SunkUntil==0,false,now,ready) || Cannonballs.Count+PendingCrewShots > 509) return;
        if (starboard) vessel.StarboardReady = now + _settings.CannonReloadMilliseconds*(ammo?.ReloadPercent??100)/100;
        else vessel.PortReady = now + _settings.CannonReloadMilliseconds*(ammo?.ReloadPercent??100)/100;
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
            if(planned==null)Emit(NavalEffectKind.Fire,map,x,y,ShipMovement.Normalize(s.Heading+(starboard?90:-90)),now,!sounded);sounded=true;
            (planned??Cannonballs).Add(new() { Ammo=ammo,ImpactAnimationId=ammo?.ImpactAnimationId??_settings.Ammunition.FirstOrDefault(a=>a.Kind==CannonAmmoKind.Standard)?.ImpactAnimationId??Guid.Empty,CaptainId=s.CaptainId,Enemy=vessel.Enemy!=null,DockSafeRange=vessel.Enemy?.DockSafeRange??0,Born=now,Heading=ShipMovement.Normalize(s.Heading+(starboard?90:-90)),ShipId=s.Id,OwnerId=vessel.OwnerId,MapId=map,X=x,Y=y,
                DirectionX=direction.X,DirectionY=direction.Y,Remaining=Math.Min(80,_settings.CannonRange*(ammo?.RangePercent??100)/100d),Speed=_settings.CannonSpeed,Damage=vessel.Enemy?.CannonDamage??Math.Clamp(_settings.CannonDamage*(ammo?.DamagePercent??100)/100*(100+SkillPower(vessel,NavalSkillKind.OpenFire,now))/100,1,900) });
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
                var height=ball.Flight?.HeightAtDistance(ball.Travelled+distance)??0;
                var tile=(map,(int)Math.Floor(x),(int)Math.Floor(y));
                if (!hulls.TryGetValue(tile,out var target) || target.State.Id==ball.ShipId || target.SunkUntil>0 ||
                    height>Math.Max(2,target.Definition.Length)) return true;
                impact=NavalEffectKind.HullImpact;
                var permitted=ShipEnemySteering.CanDamage(ball.Enemy,target.Enemy!=null,target.OwnerId!=Guid.Empty || target.State.CaptainId!=Guid.Empty,
                    ball.OwnerId!=Guid.Empty && ball.OwnerId==target.OwnerId,_settings.PlayerShipDamage);
                if(ball.Enemy && (DockProtected(map,x,y,ball.DockSafeRange) ||
                    DockProtected(target.State.MapId,target.State.X,target.State.Y,ball.DockSafeRange))) permitted=false;
                if(permitted && Ships.TryGetValue(ball.ShipId,out var attacker))
                {
                    Engage(attacker,target,now);
                    if(ball.GunnerId==Guid.Empty && !ball.Enemy){QueueNavalTask(Driver(attacker),NavalTaskKind.CannonHits,1);QueueNavalTask(Driver(attacker),NavalTaskKind.HullDamage,Math.Min(target.Health,ball.Damage));}
                    if(ball.Ammo is {SlowPercent:>0} slow){target.SlowPercent=slow.SlowPercent;target.SlowUntil=now+slow.SlowSeconds*1000L;}
                }
                // Protected player hulls still intercept shots; no firing through another ship.
                if (permitted && target.OwnerId==Guid.Empty)
                {
                    CreditGunner(ball,target,Math.Min(target.Health,ball.Damage));
                    if(target.Enemy!=null && Ships.TryGetValue(ball.ShipId,out var firingShip))
                    {var actor=ball.GunnerId==Guid.Empty?Driver(firingShip):ball.GunnerId;target.Contributions[actor]=target.Contributions.GetValueOrDefault(actor)+Math.Min(target.Health,ball.Damage);}
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
                    target.PendingDamage=Math.Min(100,target.PendingDamage+Math.Max(1,ball.Damage*100/(100+SkillPower(target,NavalSkillKind.TakeCover,now))));
                stopped=true;return false;
            });
            ball.Remaining-=travelled;ball.Travelled+=travelled;
            if (stopped || ball.Remaining<=0) {Emit(impact,ball.MapId,ball.X,ball.Y,ball.Heading,now,animationId:ball.ImpactAnimationId,graphic:impact==NavalEffectKind.WaterImpact?ball.Ammo?.WaterGraphic:ball.Ammo?.HullGraphic);Cannonballs.Remove(ball);}
        }
        // Unoccupied public test targets disappear after their sinking animation until server restart/configuration reload.
        foreach (var vessel in Ships.Values.Where(v=>v.Enemy==null && v.OwnerId==Guid.Empty && v.State.CaptainId==Guid.Empty && v.SunkUntil>0 && now>=v.SunkUntil).ToArray())
            Ships.Remove(vessel.State.Id);
    }
}
