using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.EquipmentLoadouts;
using Intersect.Server.Database;
using Intersect.Server.Entities.Events;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    private ItemProperties? _attackingEquipmentProperties;
    private sealed record EquipmentCast(Guid ItemId, ItemProperties Properties, Guid SpellId);
    private EquipmentCast? _equipmentCast;
    private bool _equipmentProc;

    internal ItemProperties? CaptureWeaponProperties(ItemDescriptor? source) => source != null &&
        TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var item) && item.ItemId == source.Id ? new ItemProperties(item.Properties) : null;

    internal void QueueEquipmentEvent(Item? item, ItemEventTrigger trigger)
    {
        if (item?.Properties.Enhancement is {} enhancement && enhancement.EventId != Guid.Empty && enhancement.EventTrigger == trigger &&
            EventDescriptor.Get(enhancement.EventId) is { CommonEvent: true } descriptor) EnqueueStartCommonEvent(descriptor);
    }

    private void OnEquipmentWeaponHit(Entity target)
    {
        if (_equipmentProc || target is Resource or EventPageInstance || IsDead || HasPendingDeath) return;
        if (_attackingEquipmentProperties?.Enhancement is { EventTrigger: ItemEventTrigger.OnHit } enhancement &&
            EventDescriptor.Get(enhancement.EventId) is { CommonEvent: true } evt) EnqueueStartCommonEvent(evt);
        var binding = LastAttackingWeapon?.EffectiveWeaponSpell(_attackingEquipmentProperties);
        if (binding is not { Trigger: WeaponSpellTrigger.OnHit } || binding.ChancePercent < 1 || Random.Shared.Next(100) >= Math.Min(100, binding.ChancePercent) ||
            CastTime != 0 || SpellDescriptor.Get(binding.SpellId) is not {} spell ||
            !spell.IgnoreGlobalCooldown && mGlobalCooldownTimer > Timing.Global.MillisecondsUtc) return;
        var spellTarget = spell.Combat.TargetType == SpellTargetType.Self ? this : target;
        // The proc uses the usual spell costs, requirements, targeting and cooldowns; no permanent teaching.
        if (!base.CanCastSpell(spell, spellTarget, true, false, out _) || !Conditions.MeetsConditionLists(spell.CastingRequirements, this, null)) return;
        var previousTarget = CastTarget;
        try { _equipmentProc = true; CastTarget = spellTarget; CastSpell(spell.Id); }
        finally { CastTarget = previousTarget; _equipmentProc = false; }
    }

    internal bool BeginEquipmentSpell(Entity? target)
    {
        if (!InGame || IsDead || HasPendingDeath || IsBusy() || CastTime != 0 || HasBlockingEventHold ||
            !TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var item) ||
            item.Descriptor.EffectiveWeaponSpell(item.Properties) is not { Trigger: WeaponSpellTrigger.Manual } binding ||
            SpellDescriptor.Get(binding.SpellId) is not {} spell || !Conditions.MeetsConditionLists(item.Descriptor.UsageRequirements, this, null)) return false;
        if (spell.Combat.TargetType == SpellTargetType.Self) target = this;
        if (!CanCastSpell(spell, target!, true, false, out _)) return false;
        _equipmentCast = new(item.ItemId, new ItemProperties(item.Properties), spell.Id);
        SpellCastSlot = -1; CastTarget = target!; SoftRetargetOnSelfCast = false;
        CastTime = Timing.Global.Milliseconds + Math.Max(1, spell.CastDuration);
        foreach (var status in CachedStatuses.Where(s => s.Type == SpellEffect.Stealth)) status.RemoveStatus();
        if (spell.CastAnimationId != Guid.Empty)
            PacketSender.SendAnimationToProximity(spell.CastAnimationId, 1, Id, MapId, 0, 0, Dir, MapInstanceId, AnimationSourceType.SpellCast, spell.Id);
        PacketSender.SendEntityCastTime(this, spell.Id);
        SendPacket(new EquipmentFeedbackPacket { Cue = "spell" });
        return true;
    }

    private void UpdateEquipmentSpell()
    {
        if (_equipmentCast is not {} pending) return;
        if (CastTime == 0 || IsDead || HasPendingDeath || !TryGetEquippedItem(Options.Instance.Equipment.WeaponSlot, out var item) ||
            item.ItemId != pending.ItemId || !item.Properties.StatModifiers.SequenceEqual(pending.Properties.StatModifiers) ||
            !EquipmentEnhancement.Same(item.Properties.Enhancement, pending.Properties.Enhancement) ||
            item.Descriptor.EffectiveWeaponSpell(item.Properties)?.SpellId != pending.SpellId)
        { _equipmentCast = null; CastTime = 0; CastTarget = null!; return; }
        if (IsCasting) return;
        _equipmentCast = null; CastTime = 0;
        if (SpellDescriptor.Get(pending.SpellId) is {} spell && CanCastSpell(spell, CastTarget, true, false, out _)) CastSpell(spell.Id);
        CastTarget = null!;
    }
}
