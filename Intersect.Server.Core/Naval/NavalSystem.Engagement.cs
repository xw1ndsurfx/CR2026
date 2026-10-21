using Intersect.Framework.Core.Naval;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed record CargoAward(Guid ShipId,int Capacity,NavalCargoParcel Parcel);
    private sealed record SailingReward(long Experience,long RetryAt=0,CargoAward[] Cargo=null,long SailingExperience=0);
    private static readonly Dictionary<Guid,SailingReward> PendingSailingRewards=[];

    // Gate is held. Credit the captain who fired the killing shot, only if still at this helm.
    // Respawn constructs a new Vessel, so a second death earns its own single reward.
    private static void CreditEnemySink(Vessel victim,Guid attackerId,Guid captainId)
    {
        if(victim.Enemy==null || victim.Health!=0 || victim.RewardClaimed) return;
        victim.RewardClaimed=true;
        foreach(var ship in Ships.Values.Where(s=>s.CombatTargetId==victim.State.Id))
        {ship.CombatTargetId=Guid.Empty;ship.CombatUntil=0;}
        if(captainId==Guid.Empty || !Ships.TryGetValue(attackerId,out var attacker) || attacker.Enemy!=null ||
            attacker.State.CaptainId!=captainId) return;
        var now=Intersect.Framework.Core.Timing.Global.Milliseconds;
        var loot=NavalCargo.Roll(victim.Enemy.Loot,Random.Shared.NextDouble,Random.Shared.Next);
        var participants=new[]{captainId}.Concat(attacker.Crew.Where(m=>now-m.Value.Seen<=1500).Select(m=>m.Key)).Distinct().ToArray();
        var leader=participants.Contains(Driver(attacker))?Driver(attacker):captainId;
        var weights=participants.ToDictionary(id=>id,id=>victim.Contributions.GetValueOrDefault(id));
        // The current helmsman receives a twofold leadership weight, which guarantees
        // the captain is favoured without creating additional rewards.
        weights[leader]=Math.Max(1,weights.Values.DefaultIfEmpty(0).Max())*2;
        var experience=NavalRewardSharing.Divide(victim.Enemy.SailingExperienceReward,weights);
        var awards=participants.ToDictionary(id=>id,_=>new List<CargoAward>());
        var cargoQuota=NavalRewardSharing.Divide(loot.Sum(p=>(long)p.Quantity),weights);
        foreach(var parcel in loot)
            foreach(var share in NavalRewardSharing.Divide(parcel.Quantity,cargoQuota).Where(s=>s.Value>0))
            {
                cargoQuota[share.Key]-=share.Value;
                awards[share.Key].Add(new(attackerId,attacker.Definition.CargoCapacity,new NavalCargoParcel {
                    ItemId=parcel.ItemId,Quantity=(int)share.Value,Quality=parcel.Quality,Kind=parcel.Kind}));
            }
        foreach(var participant in participants.Where(id=>weights[id]>0))
        {
            QueueNavalTask(participant,NavalTaskKind.ShipsSunk,1);
            PendingSailingRewards.TryGetValue(participant,out var pending);
            PendingSailingRewards[participant]=new(Math.Min(200000000000000L,(pending?.Experience??0)+experience.GetValueOrDefault(participant)),
                Cargo:(pending?.Cargo??[]).Concat(awards[participant]).Take(6400).ToArray(),
                SailingExperience:Math.Min(200000000000000L,(pending?.SailingExperience??0)+(participant==leader?victim.Enemy.SailingExperienceReward:0)));
        }
    }

    // Player lock is held before Gate. Never save a different captain from the world/combat loop.
    private static bool ApplyPendingExperience(Player player,long now,bool force=false)
    {
        SailingReward reward;
        lock(Gate)
        {
            if(!PendingSailingRewards.TryGetValue(player.Id,out var pending)) return true;
            if(!force && now<pending.RetryAt) return false;
            reward=pending;PendingSailingRewards.Remove(player.Id);
        }
        var before=_settings.Sailing.Progress(player.SailingExperience).Level;
        var saved=false;NavalRewardReceipt receipt=null;
        try {saved=CommitNavalReward(player,reward.Experience,(reward.Cargo??[]).Select(c=>(c.ShipId,c.Capacity,c.Parcel)),()=>player.User?.Save(force:true)==UserSaveResult.Completed,out receipt,reward.SailingExperience);}
        catch(Exception exception) { ApplicationContext.Context.Value?.Logger.LogError(exception,"Could not save Sailing reward for {PlayerId}",player.Id); }
        finally
        {
            lock(Gate)
            {
                if(!saved)
                {
                    PendingSailingRewards.TryGetValue(player.Id,out var concurrent);
                    PendingSailingRewards[player.Id]=new(Math.Min(200000000000000L,reward.Experience+(concurrent?.Experience??0)),now+1000,(reward.Cargo??[]).Concat(concurrent?.Cargo??[]).Take(6400).ToArray(),
                        Math.Min(200000000000000L,reward.SailingExperience+(concurrent?.SailingExperience??0)));
                }
                else EmitLevelUp(player,before,_settings.Sailing.Progress(player.SailingExperience).Level,now);
            }
        }
        if(saved && receipt!=null) PublishRewardReceipt(player,receipt,now);
        return saved;
    }

    internal static bool CommitSailingExperience(Player player,long earned,Func<bool> save)
    {
        if(earned<=0) return true;
        var previous=player.SailingExperience;
        if(previous>=_settings.Sailing.Cap) return true; // Preserve XP above a subsequently lowered cap.
        var committed=false;
        try
        {
            player.SailingExperience=Math.Min(_settings.Sailing.Cap,previous+Math.Min(earned,200000000000000L));
            committed=save();return committed;
        }
        finally {if(!committed) player.SailingExperience=previous;}
    }

    private static void Engage(Vessel attacker,Vessel target,long now)
    {
        if(attacker.Enemy==null && attacker.State.CaptainId!=Guid.Empty && target.Enemy!=null)
        {attacker.CombatTargetId=target.State.Id;attacker.CombatUntil=now+_settings.CombatTimeoutSeconds*1000L;}
        else if(attacker.Enemy!=null && target.Enemy==null && target.State.CaptainId!=Guid.Empty)
        {
            // Keep the player's chosen outgoing target; incoming shots refresh that same engagement.
            if(target.CombatTargetId==attacker.State.Id || target.CombatUntil<=now ||
                !Ships.TryGetValue(target.CombatTargetId,out var old) || old.SunkUntil>0)
            {target.CombatTargetId=attacker.State.Id;target.CombatUntil=now+_settings.CombatTimeoutSeconds*1000L;}
        }
    }

    private static Guid CombatTarget(Guid ownId,long now,HashSet<Guid> visible)
    {
        if(!Ships.TryGetValue(ownId,out var own)) return Guid.Empty;
        if(own.State.CaptainId==Guid.Empty || own.SunkUntil>0 || own.CombatUntil<=now ||
            !Ships.TryGetValue(own.CombatTargetId,out var target) || target.Enemy==null || target.SunkUntil>0 || target.Health<=0 ||
            !visible.Contains(target.State.MapId) || SafeDock(target,own.State.MapId,own.State.X,own.State.Y))
        {own.CombatTargetId=Guid.Empty;own.CombatUntil=0;return Guid.Empty;}
        return target.State.Id;
    }
}
