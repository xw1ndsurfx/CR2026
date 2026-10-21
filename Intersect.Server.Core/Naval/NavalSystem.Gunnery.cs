using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private sealed class GunSeat {internal Guid Owner,Lease,Reservation;internal long Until,ReadyAt;}
    private sealed record GunReward(long Experience,long RetryAt=0);
    private static readonly Dictionary<Guid,GunReward> PendingGunnery=[];
    private static int PendingCrewShots;
    private static bool GunnerEligible(Player player,Vessel vessel)=>!player.NavalPartyExitRequested && player.NavalShipId==vessel.State.Id && Driver(vessel)!=player.Id &&
        vessel.State.CaptainId!=Guid.Empty && (player.Id==vessel.State.CaptainId || player.Party.Any(m=>m.Id==vessel.State.CaptainId)) && vessel.Enemy==null && vessel.SunkUntil==0 && vessel.Health>0 && !Repairs.ContainsKey(player.Id) &&
        (vessel.State.CaptainId==player.Id || vessel.Crew.TryGetValue(player.Id,out var crew) && Timing.Global.Milliseconds-crew.Seen<=1500);
    private static void PruneGuns(Vessel vessel,long now)
    {
        foreach(var entry in vessel.Guns)
            if(entry.Key>=_settings.Gunnery.CannonsPerSide*2 || entry.Value.Until<=now || vessel.SunkUntil>0 ||
                vessel.State.CaptainId==Guid.Empty || entry.Value.Owner==Driver(vessel) || Repairs.ContainsKey(entry.Value.Owner) ||
                entry.Value.Owner!=vessel.State.CaptainId && (!vessel.Crew.TryGetValue(entry.Value.Owner,out var crew) || now-crew.Seen>1500))
            {entry.Value.Owner=entry.Value.Lease=Guid.Empty;entry.Value.Until=0;}
    }
    private static GunneryState GunnerState(Player player,long now)
    {
        var settings=_settings.Gunnery;var progress=settings.Curve().Progress(player.GunneryExperience);
        var result=new GunneryState {Level=progress.Level,Experience=progress.Experience,Required=progress.Required,
            CannonsPerSide=settings.CannonsPerSide,AimArcDegrees=settings.AimArcDegrees,Message=player.GunneryMessage??"",
            Enabled=_settings.CannonsEnabled && settings.Enabled};
        if(!Ships.TryGetValue(player.NavalShipId,out var vessel) || !GunnerEligible(player,vessel)) return result;
        PruneGuns(vessel,now);result.ShipId=vessel.State.Id;
        var occupied=vessel.Guns.FirstOrDefault(g=>g.Value.Owner==player.Id);
        if(occupied.Value!=null)
        {
            result.LeaseId=occupied.Value.Lease;result.Station=occupied.Key%settings.CannonsPerSide;
            result.Starboard=occupied.Key>=settings.CannonsPerSide;
            result.ReloadMilliseconds=(int)Math.Clamp(occupied.Value.ReadyAt-now,0,180000);
        }
        result.Ammunition=_settings.Ammunition.Select(a=>new CannonAmmoView {Icon=a.Icon,Kind=a.Kind,RequiredLevel=a.RequiredLevel,
            DamagePercent=a.DamagePercent,RangePercent=a.RangePercent,ReloadPercent=a.ReloadPercent,
            Quantity=a.RequiresItem && a.ItemId!=Guid.Empty && ItemDescriptor.Get(a.ItemId)?.IsStackable==true?AmmoCount(player,a.ItemId)/a.UnitsPerShot:a.RequiresItem?0:-1,
            Available=progress.Level>=a.RequiredLevel && (!a.RequiresItem || a.ItemId!=Guid.Empty &&
                ItemDescriptor.Get(a.ItemId)?.IsStackable==true && AmmoCount(player,a.ItemId)>=a.UnitsPerShot)}).ToArray();
        return result;
    }
    internal static void Station(Player player,CannonStationPacket packet,long now)
    {
        lock(player.EntityLock)
        {
            Cannonball shot=null;GunSeat reserved=null;CannonAmmunition ammo=null;long previousReady=0;Guid reservation=Guid.Empty;
            lock(Gate)
            {
                if(!IsEnabled() || !player.InGame || player.IsDead || player.HasPendingDeath || player.NavalBusy || player.Z!=0 ||
                    player.InstanceType!=MapInstanceType.Overworld || !Enum.IsDefined(packet.Action) ||
                    !Ships.TryGetValue(packet.ShipId,out var vessel) || player.MapId!=vessel.State.MapId || !GunnerEligible(player,vessel)) return;
                if(now-player.LastGunneryRequest<100) return;
                player.LastGunneryRequest=now;PruneGuns(vessel,now);
                var count=_settings.Gunnery.CannonsPerSide;
                var current=vessel.Guns.FirstOrDefault(g=>g.Value.Owner==player.Id);
                if(packet.Action==CannonStationAction.Release)
                {
                    if(current.Value!=null && current.Value.Lease==packet.LeaseId) current.Value.Owner=current.Value.Lease=Guid.Empty;
                    SendSnapshot(player);return;
                }
                if(!_settings.Gunnery.Enabled || !_settings.CannonsEnabled) {player.GunneryMessage="Crew cannons are disabled.";SendSnapshot(player);return;}
                if(packet.Action==CannonStationAction.Acquire)
                {
                    var start=packet.Starboard?count:0;
                    var index=Enumerable.Range(start,count).FirstOrDefault(i=>!vessel.Guns.TryGetValue(i,out var gun) || gun.Owner==Guid.Empty || gun.Owner==player.Id,-1);
                    if(index<0) player.GunneryMessage="All cannons on this side are occupied.";
                    else
                    {
                        if(current.Value!=null) current.Value.Owner=current.Value.Lease=Guid.Empty;
                        if(!vessel.Guns.TryGetValue(index,out var gun)) vessel.Guns[index]=gun=new();
                        gun.Owner=player.Id;gun.Lease=Guid.NewGuid();gun.Until=now+3500;player.GunneryMessage="";
                    }
                    SendSnapshot(player);return;
                }
                if(current.Value==null || current.Value.Lease!=packet.LeaseId || ((current.Key>=count)!=packet.Starboard)) return;
                current.Value.Until=now+3500;
                if(packet.Action==CannonStationAction.Renew) return;
                if(packet.Action!=CannonStationAction.Fire || !GunneryAim.Valid(packet.Yaw,packet.Elevation,_settings.Gunnery.AimArcDegrees) ||
                    current.Value.ReadyAt>now || Cannonballs.Count+PendingCrewShots>=512) return;
                ammo=_settings.Ammunition.FirstOrDefault(a=>a.Kind==packet.Ammunition);
                if(ammo==null || _settings.Gunnery.Curve().Progress(player.GunneryExperience).Level<ammo.RequiredLevel)
                {player.GunneryMessage="This ammunition requires a higher Gunnery level.";SendSnapshot(player);return;}
                if(ammo.RequiresItem && (ammo.ItemId==Guid.Empty || ItemDescriptor.Get(ammo.ItemId)?.IsStackable!=true || AmmoCount(player,ammo.ItemId)<ammo.UnitsPerShot))
                {player.GunneryMessage="Not enough ammunition for this cannonball.";SendSnapshot(player);return;}
                var s=vessel.State;var beam=ShipActions.Direction(s.Heading,packet.Starboard);
                var offset=GunneryAim.StationOffset(current.Key%count,count,vessel.Definition.Length);
                var angle=s.Heading*Math.PI/180;var radius=vessel.Definition.Width/2+.1;
                var map=s.MapId;var x=s.X+beam.X*radius+Math.Sin(angle)*offset;var y=s.Y+beam.Y*radius-Math.Cos(angle)*offset;
                if(!OceanMapResolver.Resolve(OceanMapResolver.Maps(),ref map,ref x,ref y) || BlocksCannonball(map,x,y))
                {player.GunneryMessage="The cannon muzzle is blocked.";SendSnapshot(player);return;}
                var heading=ShipMovement.Normalize(s.Heading+(packet.Starboard?90:-90)+packet.Yaw);var radians=heading*Math.PI/180;
                var flight=CannonFlight.Plan(packet.Elevation,_settings.CannonSpeed,Math.Min(80,_settings.CannonRange*ammo.RangePercent/100d));
                shot=new() {Ammo=ammo,ImpactAnimationId=ammo.ImpactAnimationId,Flight=flight,GunnerId=player.Id,CaptainId=s.CaptainId,ShipId=s.Id,OwnerId=vessel.OwnerId,MapId=map,X=x,Y=y,
                    DirectionX=Math.Sin(radians),DirectionY=-Math.Cos(radians),Heading=heading,Born=now,Speed=flight.HorizontalSpeed,
                    Remaining=flight.Range,
                    Damage=Math.Clamp(_settings.CannonDamage*ammo.DamagePercent/100*(100+SkillPower(vessel,NavalSkillKind.OpenFire,now))/100,1,900)};
                reserved=current.Value;previousReady=reserved.ReadyAt;
                reservation=reserved.Reservation=Guid.NewGuid();
                reserved.ReadyAt=now+(long)_settings.Gunnery.GunReloadMilliseconds*ammo.ReloadPercent/100;
                PendingCrewShots++;
            }
            // The owning player lock is held. Save ammunition outside the world lock; never lock/save another player.
            var consumed=false;
            try {consumed=!ammo.RequiresItem || CommitAmmo(player,ammo.ItemId,-ammo.UnitsPerShot,()=>player.User?.Save(force:true)==UserSaveResult.Completed);}
            catch(Exception e) {ApplicationContext.Context.Value?.Logger.LogError(e,"Could not save naval ammunition for {PlayerId}",player.Id);}
            lock(Gate)
            {
                PendingCrewShots--;
                if(!consumed) {if(reserved.Reservation==reservation) reserved.ReadyAt=previousReady;player.GunneryMessage="Ammunition could not be saved. No shot was fired.";}
                else
                {
                    Cannonballs.Add(shot);Emit(NavalEffectKind.Fire,shot.MapId,shot.X,shot.Y,shot.Heading,now);
                    player.GunneryMessage="";player.RegisterAfkActivity();
                }
                SendSnapshot(player);
            }
            if(ammo.RequiresItem) {PacketSender.SendInventory(player);if(consumed) player.RefreshNavalCargoQuests([ammo.ItemId]);}
        }
    }
    internal static bool CommitGunAmmunition(Player player,Guid itemId,int count,Func<bool> save)
    {
        if(itemId==Guid.Empty || count<1 || ItemDescriptor.Get(itemId)?.IsStackable!=true || !player.CanTakeItem(itemId,count)) return false;
        var before=SnapshotCargoInventory(player);var committed=false;var remaining=count;
        try
        {
            for(var i=0;i<before.Length && remaining>0;i++)
            {
                var slot=player.Items[i];if(slot.ItemId!=itemId) continue;
                var take=Math.Min(slot.Quantity,remaining);remaining-=take;
                if(slot.Quantity==take) slot.Set(Item.None);else slot.Quantity-=take;
            }
            if(remaining!=0) return false;
            committed=save();return committed;
        }
        finally {if(!committed) for(var i=0;i<before.Length;i++) player.Items[i].Set(before[i]);}
    }
    private static void CreditGunner(Cannonball ball,Vessel victim,int damage)
    {
        if(ball.GunnerId==Guid.Empty || victim.Enemy==null || damage<=0 || !Ships.TryGetValue(ball.ShipId,out var ship) ||
            ship.State.CaptainId!=ball.GunnerId && !ship.Crew.ContainsKey(ball.GunnerId)) return;
        QueueNavalTask(ball.GunnerId,NavalTaskKind.CannonHits,1);QueueNavalTask(ball.GunnerId,NavalTaskKind.HullDamage,damage);
        PendingGunnery.TryGetValue(ball.GunnerId,out var pending);
        PendingGunnery[ball.GunnerId]=new(Math.Min(200000000000000L,(pending?.Experience??0)+(long)damage*_settings.Gunnery.ExperiencePerDamage));
    }
    internal static bool CommitGunneryExperience(Player player,long earned,Func<bool> save)
    {
        var before=player.GunneryExperience;var committed=false;var cap=_settings.Gunnery.Curve().Cap;
        try {player.GunneryExperience=before>=cap?before:Math.Min(cap,before+Math.Clamp(earned,0,200000000000000L));committed=save();return committed;}
        finally {if(!committed) player.GunneryExperience=before;}
    }
    private static void ApplyPendingGunnery(Player player,long now)
    {
        GunReward reward;
        lock(Gate)
        {
            if(!PendingGunnery.TryGetValue(player.Id,out reward) || now<reward.RetryAt) return;
            PendingGunnery.Remove(player.Id);
        }
        var before=_settings.Gunnery.Curve().Progress(player.GunneryExperience).Level;
        var saved=false;
        try {saved=CommitGunneryExperience(player,reward.Experience,()=>player.User?.Save(force:true)==UserSaveResult.Completed);}
        catch(Exception e) {ApplicationContext.Context.Value?.Logger.LogError(e,"Could not save Gunnery reward for {PlayerId}",player.Id);}
        if(saved) EmitLevelUp(player,before,_settings.Gunnery.Curve().Progress(player.GunneryExperience).Level,now);
        else lock(Gate)
        {
            PendingGunnery.TryGetValue(player.Id,out var pending);
            PendingGunnery[player.Id]=new(Math.Min(200000000000000L,reward.Experience+(pending?.Experience??0)),now+1000);
        }
    }
}
