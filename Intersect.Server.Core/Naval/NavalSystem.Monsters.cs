using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed record MonsterAftermath(Guid Id,Guid Captain,Guid Ship,NavalMonsterDefinition Definition,bool Won,long Started,bool SinkQueued=false);
    private static readonly Dictionary<Guid,long> MonsterReady=[];
    private static readonly Dictionary<Guid,MonsterAftermath> MonsterAftermaths=[];

    // Called with the current player's entity lock. Never lock another participant while holding Gate.
    private static void UpdateNavalMonsters(Player player,long now)
    {
        EnemyBoardingRun? run=null;NavalMonsterDefinition? spawn=null;Vessel? source=null;
        lock(Gate)
        {
            foreach(var expired in MonsterAftermaths.Where(p=>!Ships.ContainsKey(p.Value.Ship)).Select(p=>p.Key).ToArray())MonsterAftermaths.Remove(expired);
            foreach(var effect in MonsterAftermaths.Values.Where(e=>e.Captain==player.Id).ToArray())
            {
                if(!Ships.TryGetValue(effect.Ship,out var ship)){MonsterAftermaths.Remove(effect.Id);continue;}
                var age=now-effect.Started;
                if(effect.SinkQueued){if(age>=NavalMonsterBattle.CrushingMilliseconds+2000)MonsterAftermaths.Remove(effect.Id);continue;}
                ship.EnemyBoardingEncounter=effect.Id;ship.Anchored=true;ship.Controls.Reset();ship.State.Speed=ship.State.Throttle=0;
                if(age<NavalMonsterBattle.CrushingMilliseconds)continue;
                ship.EnemyBoardingEncounter=Guid.Empty;
                if(effect.Won){MonsterAftermaths.Remove(effect.Id);continue;}
                if(ship.SunkUntil==0)ship.PendingDamage=Math.Max(ship.PendingDamage,100);
                MonsterAftermaths[effect.Id]=effect with {SinkQueued=true};
            }
            if(BoardingPlayers.TryGetValue(player.Id,out var id) && EnemyBoardings.TryGetValue(id,out var active) && active.Monster!=null)
            {
                if(active.Captain!=player.Id || active.Starting || active.Finished)return;
                run=active;
                if(Ships.TryGetValue(run.Ship,out source))
                {
                    var damage=run.MonsterBattle!.Advance(now,Math.Max(0,run.Monster.TentacleCount-run.DefeatedCrew.Count));
                    source.PendingDamage=Math.Min(99,source.PendingDamage+Math.Min(damage,Math.Max(0,source.Health-source.PendingDamage-1)));
                }
            }
            else if(IsEnabled() && player.InstanceType==MapInstanceType.Overworld && player.InGame && !player.IsDead && !player.HasPendingDeath && !player.NavalBusy &&
                Ships.TryGetValue(player.CaptainedShipId,out source) && source.OwnerId==player.Id && source.State.CaptainId==player.Id && DeckEnabled(source) &&
                source.Health>0 && source.SunkUntil==0 && source.EnemyBoardingEncounter==Guid.Empty && source.LeftLaunchDock && !ByCrewDock(source))
                spawn=_settings.Monsters.FirstOrDefault(m=>m.Enabled && m.MapId==source.State.MapId &&
                    now>=MonsterReady.GetValueOrDefault(m.Id) && !EnemyBoardings.Values.Any(r=>r.Monster?.Id==m.Id && !r.Finished) &&
                    Distance(source.State.X,source.State.Y,m.X,m.Y)<=m.DetectionRange &&
                    !ShipDock.Tiles(Intersect.Server.Maps.MapController.Get(source.State.MapId)?.Attributes).Any(d=>Distance(source.State.X,source.State.Y,d.X+.5,d.Y+.5)<m.DockSafeRange));
            if(spawn!=null)MonsterReady[spawn.Id]=now+10000;
        }
        if(run!=null)
        {
            ApplyPendingDamage(player,now);
            if(source==null || run.MonsterBattle!.Phase is NavalMonsterPhase.Crushing or NavalMonsterPhase.Sinking)
                FinishEnemyBoarding(run.Id,false);
        }
        else if(spawn!=null && source!=null)StartMonsterAssault(player,source,spawn,now);
    }

    private static void StartMonsterAssault(Player captain,Vessel ship,NavalMonsterDefinition definition,long now)
    {
        var monster=JsonConvert.DeserializeObject<NavalMonsterDefinition>(JsonConvert.SerializeObject(definition))!;
        var candidates=new[]{captain}.Concat(captain.Party).DistinctBy(p=>p.Id).Where(p=>p.IsOnline && p.InGame && !p.IsDead && !p.HasPendingDeath && !p.NavalBusy && p.NavalShipId==ship.State.Id).ToArray();
        lock(Gate)if(!candidates.Contains(captain) || ship.Crew.Keys.Any(id=>!candidates.Any(p=>p.Id==id)))return;
        var generated=CreateMonsterDeck(monster,ship.Definition,now);if(generated==null)return;
        var (dungeon,map,x,y)=generated.Value;
        monster.TentacleCount=map.Spawns.Count;
        EnemyBoardingRun run;
        lock(Gate)
        {
            if(EnemyBoardings.Count>=128 || ship.EnemyBoardingEncounter!=Guid.Empty || !candidates.Contains(captain) ||
                candidates.Any(p=>BoardingPlayers.ContainsKey(p.Id)) || ship.Crew.Keys.Any(id=>!candidates.Any(p=>p.Id==id)))return;
            run=new(){Ship=ship.State.Id,Captain=captain.Id,GeneratedMap=map.Id,Started=now,Monster=monster,MonsterBattle=new(monster,ship.Health,now)};
            EnemyBoardings.Add(run.Id,run);MonsterReady[monster.Id]=now+monster.RespawnSeconds*1000L;
            ship.EnemyBoardingEncounter=run.Id;ship.Controls.Reset();ship.State.Speed=ship.State.Throttle=0;
            foreach(var member in candidates)
            {
                run.Members.Add(member.Id);BoardingPlayers[member.Id]=run.Id;Repairs.Remove(member.Id);
                member.CaptainedShipId=member.BoardedShipId=Guid.Empty;
                foreach(var gun in ship.Guns.Values.Where(g=>g.Owner==member.Id))gun.Owner=gun.Lease=Guid.Empty;
            }
        }
        dungeon.MaximumPartySize=Math.Max(1,candidates.Length);dungeon.TimeLimitMinutes=Math.Clamp((monster.AssaultSeconds+NavalMonsterBattle.EmergingMilliseconds/1000+59)/60,1,31);
        var started=false;
        try
        {
            foreach(var npcId in map.Spawns.Select(s=>s.NpcId).Distinct())foreach(var member in candidates)PacketSender.SendGameObject(member.Client,NPCDescriptor.Get(npcId));
            started=DungeonRunRuntime.TryStart(captain,dungeon,map.Id,(byte)x,(byte)y,WarpDirection.Down,true,MapInstanceType.Personal,out _,candidates,run.Id);
        }
        catch(Exception error){Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(error,"Could not start naval monster {EncounterId}",run.Id);}
        finally
        {
            lock(Gate)run.Starting=false;
            if(!started)
            {
                // A transfer/configuration failure is not a gameplay defeat.
                lock(Gate){run.Finished=true;run.Won=false;MonsterReady[monster.Id]=now+10000;}
                // UpdateEnemyBoarding returns each participant under that player's own entity lock.
            }
        }
    }

    private static void FinishMonsterAssault(EnemyBoardingRun run,bool won,long now)
    {
        var monster=run.Monster!;var battle=run.MonsterBattle!;
        battle.Advance(now,won?0:Math.Max(0,monster.TentacleCount-run.DefeatedCrew.Count));
        won=won && battle.Phase==NavalMonsterPhase.Retreating;
        run.Finished=true;run.Won=won;
        MonsterReady[monster.Id]=now+monster.RespawnSeconds*1000L;
        MonsterAftermaths[run.Id]=new(run.Id,run.Captain,run.Ship,monster,won,now);
        if(!won && Ships.TryGetValue(run.Ship,out var doomed))doomed.State.DamageState=1;
        if(won && Ships.TryGetValue(run.Ship,out var source))
        {
            var victim=new Vessel(new(){Name=monster.Name,MapId=source.State.MapId}){Enemy=new(){Loot=monster.Loot,SailingExperienceReward=monster.SailingExperienceReward},Health=0};
            foreach(var member in run.Members.Where(id=>Player.FindOnline(id) is {} p && CanReceiveBoardingReward(p,run.Id)))victim.Contributions[member]=1;
            CreditNavalVictory(victim,run.Ship,run.Captain,false);
            if(monster.DeathAnimationId!=Guid.Empty)Emit(NavalEffectKind.HullImpact,source.State.MapId,source.State.X,source.State.Y,0,now,false,monster.DeathAnimationId);
        }
    }

    private static NavalMonsterSnapshot[] MonsterSnapshots(Player player,long now,IReadOnlySet<Guid> visible)
    {
        var result=new List<NavalMonsterSnapshot>();
        foreach(var run in EnemyBoardings.Values.Where(r=>r.Monster!=null && r.Members.Contains(player.Id) && !r.Starting && !r.Finished))
        {
            var battle=run.MonsterBattle!;var monster=run.Monster!;
            var scene=Intersect.Server.Maps.MapController.Get(run.GeneratedMap)?.BoardingScene;
            if(scene?.Ships.FirstOrDefault() is not {} ship)continue;
            result.Add(MonsterSnapshot(run.Id,run.Ship,run.GeneratedMap,monster,ship.AnchorX,ship.AnchorY,battle.Phase,now-battle.PhaseStarted,battle.LiveTentacles,
                Math.Max(0,monster.AssaultSeconds-(int)(Math.Max(0,now-run.Started-NavalMonsterBattle.EmergingMilliseconds)/1000)),hullHealth:battle.HullHealth));
        }
        foreach(var effect in MonsterAftermaths.Values.Where(e=>Ships.TryGetValue(e.Ship,out var s) && visible.Contains(s.State.MapId)))
            if(Ships.TryGetValue(effect.Ship,out var source))result.Add(MonsterSnapshot(effect.Id,effect.Ship,source.State.MapId,effect.Definition,source.State.X,source.State.Y,
                effect.Won?NavalMonsterPhase.Retreating:effect.SinkQueued?NavalMonsterPhase.Sinking:NavalMonsterPhase.Crushing,now-effect.Started-(effect.SinkQueued?NavalMonsterBattle.CrushingMilliseconds:0),0,0,
                DeckKind(source) switch {ShipDeckKind.ShipOfLine=>26,ShipDeckKind.Raft=>10,ShipDeckKind.Sloop=>14,_=>18}));
        if(player.InstanceType==MapInstanceType.Overworld)
            foreach(var monster in _settings.Monsters.Where(m=>m.Enabled && visible.Contains(m.MapId) && now>=MonsterReady.GetValueOrDefault(m.Id) &&
                !EnemyBoardings.Values.Any(r=>r.Monster?.Id==m.Id && !r.Finished) && !MonsterAftermaths.Values.Any(e=>e.Definition.Id==m.Id)))
                result.Add(MonsterSnapshot(monster.Id,Guid.Empty,monster.MapId,monster,monster.X,monster.Y,NavalMonsterPhase.Idle,now%int.MaxValue,monster.TentacleCount,0));
        return result.Take(64).ToArray();
    }
    private static NavalMonsterSnapshot MonsterSnapshot(Guid id,Guid ship,Guid map,NavalMonsterDefinition monster,double x,double y,NavalMonsterPhase phase,long age,int alive,int remaining,double size=14,int hullHealth=100)=>
        new(){Id=id,ShipId=ship,MapId=map,Name=string.IsNullOrWhiteSpace(monster.Name)?monster.Kind.ToString():monster.Name,Graphics=monster.Graphics,X=x,Y=y,Phase=phase,AgeMilliseconds=(int)Math.Clamp(age,0,int.MaxValue),LiveTentacles=alive,TotalTentacles=monster.TentacleCount,
            RemainingSeconds=remaining,BossAnimationId=monster.BossAnimationId,BossAnimationOffsetY=monster.BossAnimationOffsetY,SizeTiles=size,Kind=monster.Kind,HullHealth=hullHealth};
}
