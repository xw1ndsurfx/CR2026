using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Archery;
using Intersect.Server.Maps;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    private readonly Dictionary<string,long> _weaponSkillCooldowns=[];
    private long _weaponSkillRequestAt;
    private void SendWeaponSkillCooldowns(string message="")
    {
        var now=Timing.Global.Milliseconds;
        SendPacket(new ArcheryStatePacket { Message=message,WeaponSkillCooldownsJson=JsonSerializer.Serialize(
            _weaponSkillCooldowns.Where(p=>p.Value>now).ToDictionary(p=>p.Key,p=>p.Value-now)) });
    }

    internal bool HandleWeaponSkill(ArcheryRequestPacket packet)
    {
        lock(EntityLock)
        {
            var now=Timing.Global.Milliseconds;
            if(now-_weaponSkillRequestAt<100) return false;
            _weaponSkillRequestAt=now;
            if(IsDead || HasPendingDeath || IsCasting || IsBlocking || IsAttacking || IsArcheryActive || IsJoustProtected ||
                CachedStatuses.Any(s=>s.Type is SpellEffect.Stun or SpellEffect.Sleep)) return false;
            if(!RecoverMasteryRespec()) return false;
            if(!TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot,out var equipped) || equipped.Descriptor is not {} weapon) return false;
            var category=RoyalCombatRuntime.Current.Masteries.FirstOrDefault(c=>c.Key==packet.CategoryKey && c.WeaponIds.Contains(weapon.Id));
            if(category==null) return false;
            var learned=EffectiveTalents(category).FirstOrDefault(n=>n.Talent.Key==packet.TalentKey && n.Talent.Skill.Enabled);
            if(learned.Talent==null) return false;
            var key=category.Key+"/"+learned.Talent.Key;
            if(_weaponSkillCooldowns.GetValueOrDefault(key)>now) { SendWeaponSkillCooldowns(); return false; }
            if(!MapController.TryGetInstanceFromMap(MapId,MapInstanceId,out var instance)) return false;
            var target=instance.GetEntities().FirstOrDefault(e=>e.Id==packet.TargetId);
            if(target is not (Npc or Player) || target==this || target.IsDead || target.MapId!=MapId || target.MapInstanceId!=MapInstanceId ||
                GetDistanceTo(target)>learned.Talent.Skill.Range || !ValidTauntTarget(target) || !CanAttack(target,null)) return false;
            var skill=learned.Talent.Skill;
            _weaponSkillCooldowns[key]=now+skill.CooldownMilliseconds;
            LastAttackingWeapon=weapon;
            var animations=new List<KeyValuePair<Guid,Direction>>();
            if(AnimationDescriptor.Get(skill.HitAnimationId)!=null) animations.Add(new(skill.HitAnimationId,Direction.Up));
            var damage=(int)Math.Clamp((long)Math.Max(1,weapon.Damage)*skill.Power(learned.Rank)/100,1,int.MaxValue);
            var scaling=(int)Math.Clamp((long)weapon.Scaling*skill.Power(learned.Rank)/100,0,int.MaxValue);
            base.TryAttack(target,damage,(DamageType)weapon.DamageType,(Stat)weapon.ScalingStat,scaling,
                weapon.CritChance,weapon.CritMultiplier,animations,animations,weapon);
            PacketSender.SendEntityAttack(this,CalculateAttackTime());
            var animation=skill.AnimationId!=Guid.Empty?skill.AnimationId:weapon.AttackAnimationId;
            if(AnimationDescriptor.Get(animation)!=null)
                PacketSender.SendAnimationToProximity(animation,1,Id,MapId,0,0,Dir,MapInstanceId);
            SendWeaponSkillCooldowns();
            return true;
        }
    }
}
