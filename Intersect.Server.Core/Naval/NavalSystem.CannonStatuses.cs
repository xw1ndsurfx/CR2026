using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private sealed class CannonStatus
    {
        internal Cannonball Source;
        internal Guid ActorId;
        internal string Icon="";
        internal int Damage,IntervalSeconds;
        internal long NextTick,Ends;
    }

    // Called only after the authoritative hull collision and permission checks.
    private static void ApplyCannonStatus(Cannonball ball,Vessel target,Vessel attacker,long now)
    {
        if(target.Enemy==null || ball.Enemy || target.SunkUntil>0 || target.Health<=0 ||
            ball.Ammo is not {DamagePerTick:>0,DamageTickSeconds:>=1,DamageDurationSeconds:>=1} ammo)return;
        var interval=Math.Clamp(ammo.DamageTickSeconds,1,60);
        var next=now+interval*1000L;
        if(target.CannonStatuses.TryGetValue(ammo.Kind,out var existing) && existing.Ends>=now)
            next=existing.NextTick; // Repeated hits renew duration without postponing ticks or multiplying stacks.
        target.CannonStatuses[ammo.Kind]=new CannonStatus
        {
            Source=ball,ActorId=ball.GunnerId==Guid.Empty?Driver(attacker):ball.GunnerId,
            Icon=string.IsNullOrEmpty(ammo.StatusIcon)?ammo.Icon:ammo.StatusIcon,
            Damage=Math.Clamp(ammo.DamagePerTick,1,1000000),IntervalSeconds=interval,
            NextTick=next,Ends=now+Math.Clamp(ammo.DamageDurationSeconds,1,300)*1000L,
        };
    }

    private static void UpdateCannonStatuses(long now)
    {
        foreach(var target in Ships.Values.Where(v=>v.CannonStatuses.Count>0).ToArray())
        {
            if(!_settings.CannonsEnabled || target.Enemy==null || target.Health<=0 || target.SunkUntil>0 || target.EnemyBoardingEncounter!=Guid.Empty)
            {target.CannonStatuses.Clear();continue;}
            foreach(var pair in target.CannonStatuses.ToArray())
            {
                var effect=pair.Value;var ball=effect.Source;
                if(!Ships.TryGetValue(ball.ShipId,out var attacker) || attacker.SunkUntil>0 ||
                    attacker.State.CaptainId!=ball.CaptainId ||
                    ball.GunnerId!=Guid.Empty && attacker.State.CaptainId!=ball.GunnerId && !attacker.Crew.ContainsKey(ball.GunnerId))
                {target.CannonStatuses.Remove(pair.Key);continue;}
                var until=Math.Min(now,effect.Ends);
                if(until>=effect.NextTick)
                {
                    var ticks=1+(until-effect.NextTick)/(effect.IntervalSeconds*1000L);
                    effect.NextTick+=ticks*effect.IntervalSeconds*1000L;
                    Engage(attacker,target,now);
                    DamageCannonTarget(ball,target,(int)Math.Min(target.Health,ticks*effect.Damage),now,false,effect.ActorId);
                }
                if(target.Health<=0)break;
                if(now>=effect.Ends)target.CannonStatuses.Remove(pair.Key);
            }
        }
    }

    private static NavalCannonStatus[] CannonStatusSnapshot(Vessel target,long now)=>
        target.SunkUntil>0 || target.Health<=0 ? [] : target.CannonStatuses
            .Where(pair=>pair.Value.Ends>now).OrderBy(pair=>pair.Key)
            .Select(pair=>new NavalCannonStatus {Kind=pair.Key,Icon=pair.Value.Icon,
                DamagePerTick=pair.Value.Damage,TickSeconds=pair.Value.IntervalSeconds,
                RemainingMilliseconds=(int)Math.Clamp(pair.Value.Ends-now,0,300000)}).ToArray();

    private static void DamageCannonTarget(Cannonball ball,Vessel target,int requested,long now,bool cannonHit=true,Guid actorId=default)
    {
        var damage=Math.Min(target.Health,Math.Max(0,requested));
        if(damage<=0 || target.SunkUntil>0)return;
        CreditGunner(ball,target,damage,cannonHit);
        if(Ships.TryGetValue(ball.ShipId,out var attacker))
        {
            var actor=actorId!=Guid.Empty?actorId:ball.GunnerId==Guid.Empty?Driver(attacker):ball.GunnerId;
            if(ball.GunnerId==Guid.Empty && !ball.Enemy)
            {
                if(cannonHit)QueueNavalTask(actor,NavalTaskKind.CannonHits,1);
                QueueNavalTask(actor,NavalTaskKind.HullDamage,damage);
            }
            if(target.Enemy!=null && actor!=Guid.Empty)
                target.Contributions[actor]=target.Contributions.GetValueOrDefault(actor)+damage;
        }
        target.Health-=damage;
        target.State.DamageState=target.Health<target.MaximumHealth?1:0;
        if(target.Health>0)return;
        target.CannonStatuses.Clear();
        CreditEnemySink(target,ball.ShipId,ball.CaptainId);
        target.State.Speed=target.State.Throttle=0;target.Controls.Reset();
        target.SunkUntil=now+Math.Max(2000,target.Definition.FrameMilliseconds*4);
        target.SinkingGraphics=target.Definition.SinkingGraphics;
        Emit(NavalEffectKind.Sinking,target.State.MapId,target.State.X,target.State.Y,target.State.Heading,now);
    }
}
