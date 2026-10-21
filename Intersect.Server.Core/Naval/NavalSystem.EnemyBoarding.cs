using Intersect.Enums;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Network.Packets.Server;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed class EnemyBoardingRun
    {
        internal Guid Id=Guid.NewGuid(),Ship,Enemy,Captain;
        internal readonly HashSet<Guid> Members=[],DefeatedCrew=[];
        internal bool Starting=true,Finished,Won;
        internal long Started;
    }
    private static readonly Dictionary<Guid,EnemyBoardingRun> EnemyBoardings=[];
    private static readonly Dictionary<Guid,Guid> BoardingPlayers=[];
    private static bool CanBoardEnemy(Vessel ship,Vessel enemy)
    {
        var patrol=enemy.Enemy;
        return patrol!=null && patrol.BoardingDungeonId!=Guid.Empty && patrol.BoardingEntryMapId!=Guid.Empty &&
            ship.Enemy==null && ship.OwnerId!=Guid.Empty && ship.OwnerId==ship.State.CaptainId &&
            ship.Health>0 && enemy.Health>0 && ship.SunkUntil==0 && enemy.SunkUntil==0 && !enemy.RewardClaimed &&
            ship.EnemyBoardingEncounter==Guid.Empty && enemy.EnemyBoardingEncounter==Guid.Empty &&
            ship.State.MapId==enemy.State.MapId && Math.Abs(ship.State.Speed)<=1 &&
            enemy.Health*100L<=enemy.MaximumHealth*(long)patrol.BoardingHealthPercent &&
            Distance(ship.State.X,ship.State.Y,enemy.State.X,enemy.State.Y)<=patrol.BoardingRange;
    }
    private static Guid EnemyBoardingTarget(Player player)
    {
        if(!Ships.TryGetValue(player.CaptainedShipId,out var ship) || ship.State.CaptainId!=player.Id)return Guid.Empty;
        return Ships.Values.Where(enemy=>CanBoardEnemy(ship,enemy)).OrderBy(enemy=>Distance(ship.State.X,ship.State.Y,enemy.State.X,enemy.State.Y)).Select(enemy=>enemy.State.Id).FirstOrDefault();
    }
    private static void StartEnemyBoarding(Player captain,Guid targetId,long now)
    {
        if(!ApplyPendingExperience(captain,now,force:true) || !ApplyPendingDamage(captain,now,force:true))return;
        Vessel ship,enemy;HerboniaPatrol patrol;
        lock(Gate)
        {
            if(EnemyBoardings.Count>=128 || !Ships.TryGetValue(captain.CaptainedShipId,out ship) || ship.State.CaptainId!=captain.Id ||
                !Ships.TryGetValue(targetId,out enemy) || !CanBoardEnemy(ship,enemy))return;
            patrol=enemy.Enemy;
        }
        var authored=DungeonConfigurationRuntime.Current.Dungeons.FirstOrDefault(d=>d.Id==patrol.BoardingDungeonId);
        var map=MapController.Get(patrol.BoardingEntryMapId);
        if(authored==null || !authored.IsStructurallyValid || !authored.MapIds.Contains(patrol.BoardingEntryMapId) || map==null ||
            patrol.BoardingEntryX>=Options.Instance.Map.MapWidth || patrol.BoardingEntryY>=Options.Instance.Map.MapHeight ||
            !authored.MapIds.Any(id=>MapController.Get(id)?.Spawns.Count>0) || authored.CompletionRequirements==DungeonCompletionRequirement.None ||
            authored.CompletionRequirements.HasFlag(DungeonCompletionRequirement.DefeatFinalBoss) && authored.FinalBossNpcId==Guid.Empty)
        {lock(Gate)SendManagement(captain,NavalManageAction.Skills,now,"Configure the enemy boarding dungeon and deck entry in the naval editor.");return;}
        var dungeon=JsonConvert.DeserializeObject<DungeonDefinition>(JsonConvert.SerializeObject(authored))!;
        dungeon.NpcRespawnEnabled=false;dungeon.TimeLimitMinutes=Math.Clamp(dungeon.TimeLimitMinutes==0?5:dungeon.TimeLimitMinutes,1,20);
        var candidates=new[]{captain}.Concat(captain.Party).DistinctBy(p=>p.Id).Where(p=>p.IsOnline && p.InGame && !p.IsDead && !p.HasPendingDeath && p.NavalShipId==ship.State.Id && !p.NavalBusy).ToArray();
        EnemyBoardingRun run;
        lock(Gate)
        {
            if(!CanBoardEnemy(ship,enemy) || candidates.Any(p=>BoardingPlayers.ContainsKey(p.Id)) || !candidates.Contains(captain))return;
            // No passengers are silently left behind during the transfer.
            if(ship.Crew.Keys.Any(id=>!candidates.Any(p=>p.Id==id)))return;
            run=new(){Ship=ship.State.Id,Enemy=enemy.State.Id,Captain=captain.Id,Started=now};
            EnemyBoardings.Add(run.Id,run);
            ship.EnemyBoardingEncounter=enemy.EnemyBoardingEncounter=run.Id;
            foreach(var vessel in new[]{ship,enemy}){vessel.State.Speed=vessel.State.Throttle=0;vessel.Controls.Reset();}
            foreach(var player in candidates)
            {
                run.Members.Add(player.Id);BoardingPlayers[player.Id]=run.Id;Repairs.Remove(player.Id);
                // IDs are transient; the persisted fleet/passage stays at sea for crash recovery.
                player.CaptainedShipId=player.BoardedShipId=Guid.Empty;
                foreach(var gun in ship.Guns.Values.Where(g=>g.Owner==player.Id))gun.Owner=gun.Lease=Guid.Empty;
            }
        }
        var started=false;
        try
        {
            started=DungeonRunRuntime.TryStart(captain,dungeon,patrol.BoardingEntryMapId,(byte)patrol.BoardingEntryX,(byte)patrol.BoardingEntryY,
                WarpDirection.Down,true,MapInstanceType.Personal,out _,candidates,run.Id);
        }
        catch(Exception error){ApplicationContext.Context.Value?.Logger.LogError(error,"Could not start naval boarding {EncounterId}",run.Id);}
        lock(Gate)run.Starting=false;
        if(!started)
        {
            FinishEnemyBoarding(run.Id,false);
            foreach(var member in candidates)ReturnFromEnemyBoarding(member,run.Id,false);
            lock(Gate)SendManagement(captain,NavalManageAction.Skills,now,"Boarding could not start. Check the deck, party requirements and dungeon configuration.");
        }
    }
    internal static bool CanReceiveBoardingReward(Player player,Guid encounter)
    {
        lock(Gate)return EnemyBoardings.TryGetValue(encounter,out var run) && run.Members.Contains(player.Id) && !player.NavalPartyExitRequested &&
            (player.Id==run.Captain || player.Party.Any(p=>p.Id==run.Captain));
    }
    internal static void CreditBoardingKill(Guid encounter,Guid actor,Guid enemyId)
    {
        lock(Gate)
        {
            if(!EnemyBoardings.TryGetValue(encounter,out var run) || run.Finished || !run.Members.Contains(actor) || !run.DefeatedCrew.Add(enemyId) || !Ships.TryGetValue(run.Enemy,out var target))return;
            target.Contributions[actor]=target.Contributions.GetValueOrDefault(actor)+10;
        }
    }
    internal static void FinishEnemyBoarding(Guid encounter,bool won)
    {
        lock(Gate)
        {
            if(!EnemyBoardings.TryGetValue(encounter,out var run) || run.Finished)return;
            if(!run.Members.Contains(run.Captain) || Player.FindOnline(run.Captain) is {NavalPartyExitRequested:true})won=false;
            if(Ships.TryGetValue(run.Ship,out var source))
                foreach(var id in source.Crew.Keys.ToArray())if(Player.FindOnline(id) is {} member && !CanReceiveBoardingReward(member,encounter))source.Crew.Remove(id);
            run.Finished=true;run.Won=won;
            if(Ships.TryGetValue(run.Enemy,out var enemy))
            {
                enemy.EnemyBoardingEncounter=Guid.Empty;
                if(won && enemy.Health>0 && !enemy.RewardClaimed && Ships.TryGetValue(run.Ship,out var attacker))
                {
                    enemy.Health=0;enemy.State.Speed=enemy.State.Throttle=0;enemy.Controls.Reset();
                    CreditEnemySink(enemy,run.Ship,run.Captain);
                    enemy.SunkUntil=Timing.Global.Milliseconds+Math.Max(2000,enemy.Definition.FrameMilliseconds*4);
                    enemy.SinkingGraphics=enemy.Definition.SinkingGraphics;
                    Emit(NavalEffectKind.Sinking,enemy.State.MapId,enemy.State.X,enemy.State.Y,enemy.State.Heading,Timing.Global.Milliseconds);
                }
                else if(!won && enemy.Enemy!=null)enemy.Health=Math.Min(enemy.MaximumHealth,Math.Max(enemy.Health,enemy.MaximumHealth*enemy.Enemy.BoardingHealthPercent/100+1));
            }
        }
    }
    internal static bool ReturnFromEnemyBoarding(Player player,Guid encounter,bool revive)
    {
        EnemyBoardingRun run;Vessel ship;ShipState anchor=null;bool forfeit;
        lock(Gate)
        {
            if(!EnemyBoardings.TryGetValue(encounter,out run) || !run.Members.Contains(player.Id))return false;
            forfeit=player.NavalPartyExitRequested || player.Id!=run.Captain && !player.Party.Any(p=>p.Id==run.Captain);
        }
        if(forfeit && !CommitNavalForfeit(player,run.Ship,()=>SaveManagement(player)))return true;
        lock(Gate)
        {
            if(!EnemyBoardings.TryGetValue(encounter,out run) || !run.Members.Contains(player.Id))return false;
            forfeit=player.NavalPartyExitRequested || player.Id!=run.Captain && !player.Party.Any(p=>p.Id==run.Captain);
            if(!forfeit && Ships.TryGetValue(run.Ship,out ship) && ship.Health>0 && ship.SunkUntil==0)
            {
                if(player.Id==run.Captain){player.CaptainedShipId=run.Ship;ship.EnemyBoardingEncounter=Guid.Empty;ship.Anchored=true;ship.LastStep=0;}
                else{player.BoardedShipId=run.Ship;ship.Crew[player.Id]=new(player.Name,Timing.Global.Milliseconds);}
                anchor=CopyCrewAnchor(ship);
            }
            if(forfeit)
            {
                if(Ships.TryGetValue(run.Ship,out var formerShip))formerShip.Crew.Remove(player.Id);
                if(PendingSailingRewards.TryGetValue(player.Id,out var pending))PendingSailingRewards[player.Id]=pending with {Cargo=[]};
            }
            run.Members.Remove(player.Id);BoardingPlayers.Remove(player.Id);
            if(run.Members.Count==0)
            {
                if(!run.Finished)FinishEnemyBoarding(encounter,false);
                if(Ships.TryGetValue(run.Ship,out ship)){ship.EnemyBoardingEncounter=Guid.Empty;ship.Anchored=true;ship.LastStep=0;}
                EnemyBoardings.Remove(encounter);
            }
        }
        if(revive && player.IsDead){player.Reset();Intersect.Server.Networking.PacketSender.SendEntityDataToProximity(player);}
        if(anchor!=null)
        {
            player.NavalWarpInProgress=true;
            try{player.Warp(anchor.MapId,(int)Math.Floor(anchor.X),(int)Math.Floor(anchor.Y),player.Dir,mapInstanceType:MapInstanceType.Overworld);}
            finally{player.NavalWarpInProgress=false;}
        }
        else
        {
            if(player.Id==run.Captain){lock(Gate){player.CaptainedShipId=run.Ship;ReleaseLocked(player);}}
            var home=player.Id==run.Captain?player.NavalFleet.FirstOrDefault(s=>s.Id==run.Ship):player.NavalCrewPassage?.Ship;
            if(home!=null){ReturnToHarbour(player,home);home.AtSea=false;SaveManagement(player);}else player.WarpToSpawn();
            player.NavalPartyExitRequested=false;
        }
        lock(Gate)SendSnapshot(player);
        return true;
    }
    internal static bool RecoverFinishedEnemyBoarding(Player player)
    {
        Guid encounter;lock(Gate)if(!BoardingPlayers.TryGetValue(player.Id,out encounter))return false;
        FinishEnemyBoarding(encounter,false);
        return ReturnFromEnemyBoarding(player,encounter,player.IsDead);
    }
    private static void PruneEnemyBoardings(long now)
    {
        foreach(var run in EnemyBoardings.Values.Where(r=>r.Finished && now-r.Started>25*60000L).ToArray())
        {
            foreach(var id in run.Members.Where(id=>Player.FindOnline(id)==null).ToArray())
            {
                run.Members.Remove(id);BoardingPlayers.Remove(id);
                if(Ships.TryGetValue(run.Ship,out var source))
                {
                    source.Crew.Remove(id);
                    if(id==run.Captain)source.State.CaptainId=Guid.Empty;
                }
            }
            if(run.Members.Count==0)
            {
                if(Ships.TryGetValue(run.Ship,out var source)){source.EnemyBoardingEncounter=Guid.Empty;if(source.State.CaptainId==Guid.Empty && source.Crew.Count==0)Ships.Remove(run.Ship);}
                EnemyBoardings.Remove(run.Id);
            }
        }
    }
    internal static bool IsEnemyBoarding(Player player){lock(Gate)return BoardingPlayers.ContainsKey(player.Id);}
    private static bool UpdateEnemyBoarding(Player player,long now)
    {
        EnemyBoardingRun run;
        lock(Gate)
        {
            if(!BoardingPlayers.TryGetValue(player.Id,out var encounter) || !EnemyBoardings.TryGetValue(encounter,out run))return false;
            if(Ships.TryGetValue(run.Ship,out var ship) && player.Id!=run.Captain)ship.Crew[player.Id]=new(player.Name,now);
        }
        if(run.Starting)return true;
        if(player.NavalPartyExitRequested || player.Id!=run.Captain && !player.Party.Any(p=>p.Id==run.Captain))
        {
            DungeonRunRuntime.LeaveNavalBoarding(player,run.Id);
            ReturnFromEnemyBoarding(player,run.Id,player.IsDead);return true;
        }
        if(run.Finished){ReturnFromEnemyBoarding(player,run.Id,player.IsDead);return true;}
        if(!DungeonRunRuntime.IsPlayerInActiveRun(player))
        {
            // Also handles a run that expired while this player was disconnected.
            FinishEnemyBoarding(run.Id,false);ReturnFromEnemyBoarding(player,run.Id,player.IsDead);return true;
        }
        if(now-player.NavalSnapshotTime>=500){player.NavalSnapshotTime=now;lock(Gate)SendSnapshot(player);}
        return true;
    }
}
