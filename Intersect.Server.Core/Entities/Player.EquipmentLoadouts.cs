using Intersect.Core;
using Intersect.GameObjects;
using Intersect.Server.Entities.Events;
using Intersect.Enums;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using static Intersect.Framework.Core.Timing;
using Intersect.Network.Packets.EquipmentLoadouts;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    private long _equipmentLoadoutMutationAt;
    private EquipmentLoadoutProfile Loadouts => (PetCollection ??= new()).EquipmentLoadouts ??= new();

    internal string EquipmentLoadoutBlockReason()
    {
        if (CombatTimer > Global.Milliseconds || Naval.NavalSystem.EquipmentChangeBlocked(this)) return "Equipment sets cannot be changed during combat.";
        if (IsDead || HasPendingDeath || IsCasting || !CanUseLobbyTravel || HasBlockingEventHold || MoveRoute != null ||
            CachedStatuses.Any(s => s.Type is SpellEffect.Stun or SpellEffect.Sleep))
            return "Finish your current action before changing equipment sets.";
        return "";
    }

    internal EquipmentLoadout CaptureEquipmentLoadout(string name) => new()
    {
        Name = name.Trim(), SlotNames = Options.Instance.Equipment.Slots.ToArray(),
        Items = Equipment.Select(index => index >= 0 && index < Items.Count && Items[index] is { Quantity: > 0 } item && item.Descriptor?.ItemType == ItemType.Equipment
            ? new EquipmentLoadoutItem { ItemId = item.ItemId, StatModifiers = item.Properties.StatModifiers.ToArray(), Enhancement = item.Properties.Enhancement?.Copy() } : null).ToArray(),
    };

    internal string PlanEquipmentLoadout(EquipmentLoadout set, out int[] planned)
    {
        planned = [];
        if (!EquipmentLoadoutRules.Valid(set, Options.Instance.Equipment.Slots)) return "Equipment slots have changed. Save this set again.";
        var result = Enumerable.Repeat(-1, Equipment.Length).ToArray();
        var used = new HashSet<int>();
        for (var slot = 0; slot < set.Items.Length; slot++)
        {
            var saved = set.Items[slot]; if (saved == null) continue;
            var descriptor = ItemDescriptor.Get(saved.ItemId);
            if (descriptor?.ItemType != ItemType.Equipment || descriptor.EquipmentSlot != slot)
                return "A saved item is no longer valid for its equipment slot.";
            var found = Enumerable.Range(0, Items.Count).OrderBy(index => index == Equipment[slot] ? 0 : 1).FirstOrDefault(index =>
                !used.Contains(index) && Items[index] is { Quantity: > 0 } item && item.ItemId == saved.ItemId &&
                item.Properties?.StatModifiers?.SequenceEqual(saved.StatModifiers) == true && EquipmentEnhancement.Same(item.Properties.Enhancement, saved.Enhancement), -1);
            if (found < 0) return "A saved item is missing from your inventory or its bonuses have changed.";
            result[slot] = found; used.Add(found);
        }
        var weapon = Options.Instance.Equipment.WeaponSlot; var shield = Options.Instance.Equipment.ShieldSlot;
        if (weapon >= 0 && weapon < result.Length && shield >= 0 && shield < result.Length && result[weapon] >= 0 && result[shield] >= 0 &&
            Items[result[weapon]].Descriptor.TwoHanded) return "A two-handed weapon cannot be equipped with a shield.";
        planned = result; return "";
    }

    // The caller holds EntityLock and the account save gate. No item data is created or moved.
    // Validate requirements against the complete proposed outfit before saving or firing events.
    internal string CommitEquipmentLoadout(EquipmentLoadout set, Func<bool> save)
    {
        var blocked = EquipmentLoadoutBlockReason(); if (blocked.Length > 0) return blocked;
        var problem = PlanEquipmentLoadout(set, out var planned); if (problem.Length > 0) return problem;
        foreach (var index in planned.Where(index => index >= 0))
            if (!Conditions.MeetsConditionLists(Items[index].Descriptor.UsageRequirements, this, null))
                return "You do not meet the requirements for all items in this set.";
        if (Equipment.SequenceEqual(planned)) return "This equipment set is already equipped.";
        var before = Equipment; var vitals = VitalsJson; var committed = false;
        try
        {
            Equipment = planned;
            foreach (var index in planned.Where(index => index >= 0))
            {
                var item = Items[index].Descriptor;
                if (!Conditions.MeetsConditionLists(item.UsageRequirements, this, null))
                    return "You do not meet the requirements for all items in this set.";
                if (!before.Contains(index) && ((!item.IgnoreGlobalCooldown && mGlobalCooldownTimer > Global.MillisecondsUtc) ||
                    ItemCooldowns.TryGetValue(item.Id, out var until) && until > Global.MillisecondsUtc))
                    return "An equipment cooldown is still active. Please wait.";
            }
            FixVitals();
            committed = save();
            return committed ? "Equipment set equipped." : "Unable to save. Your equipment has not changed.";
        }
        finally { if (!committed) { Equipment = before; VitalsJson = vitals; } }
    }

    internal string CommitEquipmentLoadoutDefinition(EquipmentLoadoutRequestPacket packet, Func<bool> save)
    {
        if (!EquipmentLoadoutRules.ValidSlot(packet.Slot)) return "Choose an equipment set.";
        var profile = Loadouts;
        if (packet.Revision != profile.Revision) return "Your sets have changed. Refresh before saving.";
        if (packet.Action == EquipmentLoadoutAction.Save && (!EquipmentLoadoutRules.ValidName(packet.Name?.Trim()) || packet.Name.Any(char.IsControl)))
            return "Enter a name of 1 to 32 characters.";
        var previous = profile.Sets.GetValueOrDefault(packet.Slot); var revision = profile.Revision; var committed = false;
        try
        {
            if (packet.Action == EquipmentLoadoutAction.Save) profile.Sets[packet.Slot] = CaptureEquipmentLoadout(packet.Name);
            else if (packet.Action == EquipmentLoadoutAction.Delete) profile.Sets.Remove(packet.Slot);
            else return "Choose an equipment set.";
            profile.Revision++;
            committed = save();
            return committed ? packet.Action == EquipmentLoadoutAction.Save ? "Equipment set saved." : "Equipment set deleted." : "Unable to save. Your equipment has not changed.";
        }
        finally
        {
            if (!committed)
            {
                profile.Revision = revision;
                if (previous == null) profile.Sets.Remove(packet.Slot); else profile.Sets[packet.Slot] = previous;
            }
        }
    }

    public void HandleEquipmentLoadout(EquipmentLoadoutRequestPacket packet)
    {
        lock (EntityLock)
        {
            if (!InGame || User == null || Client == null || !Enum.IsDefined(packet.Action)) return;
            var message = "";
            if (packet.Action is not (EquipmentLoadoutAction.Open or EquipmentLoadoutAction.Refresh))
            {
                // Opening/refreshing the window must not swallow a following button click.
                // Always acknowledge a throttled mutation so the UI can leave its pending state.
                if (Environment.TickCount64 < _equipmentLoadoutMutationAt)
                {
                    SendEquipmentLoadouts(packet.RequestId, false, "Please wait a moment before changing equipment sets again.");
                    return;
                }
                _equipmentLoadoutMutationAt = Environment.TickCount64 + 300;
                var before = Equipment.ToArray();
                try
                {
                    lock (User.PokerSaveGate)
                    {
                        message = EquipmentLoadoutBlockReason();
                        if (message.Length == 0)
                            message = packet.Action == EquipmentLoadoutAction.Apply
                                ? EquipmentLoadoutRules.ValidSlot(packet.Slot) && Loadouts.Sets.TryGetValue(packet.Slot, out var set)
                                    ? CommitEquipmentLoadout(set, () => User.Save(force: true) == UserSaveResult.Completed) : "This equipment set is empty. Save it first."
                                : CommitEquipmentLoadoutDefinition(packet, () => User.Save(force: true) == UserSaveResult.Completed);
                    }
                }
                catch (Exception error)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(error, "Equipment loadout save failed for {PlayerId}", Id);
                    message = "Unable to save. Your equipment has not changed.";
                }
                // Side effects only after a committed outfit; notification failure must not undo a persisted save.
                if (message == "Equipment set equipped.")
                {
                    for (var slot = 0; slot < Equipment.Length; slot++)
                    {
                        if (before[slot] == Equipment[slot]) continue;
                        if (before[slot] >= 0) { EnqueueStartCommonEvent(Items[before[slot]].Descriptor?.GetEventTrigger(ItemEventTrigger.OnUnequip)); QueueEquipmentEvent(Items[before[slot]], ItemEventTrigger.OnUnequip); }
                        if (Equipment[slot] < 0) continue;
                        var item = Items[Equipment[slot]].Descriptor;
                        EnqueueStartCommonEvent(item.GetEventTrigger(ItemEventTrigger.OnEquip));
                        QueueEquipmentEvent(Items[Equipment[slot]], ItemEventTrigger.OnEquip);
                        if (item.Cooldown > 0) UpdateCooldown(item);
                        if (!item.IgnoreGlobalCooldown) UpdateGlobalCooldown();
                    }
                    ProcessEquipmentUpdated(true);
                }
                PacketSender.SendChatMsg(this, EquipmentLoadoutText.Get(message, Client.LanguageCode), ChatMessageType.Notice);
            }
            SendEquipmentLoadouts(packet.RequestId, packet.Action == EquipmentLoadoutAction.Open, message);
        }
    }

    private void SendEquipmentLoadouts(Guid requestId, bool open, string message)
    {
        var view = new EquipmentLoadoutView { CharacterId = Id, Revision = Loadouts.Revision, Sets = Loadouts.Sets,
            BlockReason = EquipmentLoadoutBlockReason(), Message = message };
        foreach (var (slot, set) in view.Sets)
        {
            view.Problems[slot] = PlanEquipmentLoadout(set, out _);
            foreach (var item in set.Items ?? []) if (item != null) view.ItemNames[item.ItemId] = ItemDescriptor.GetName(item.ItemId);
        }
        SendPacket(new EquipmentLoadoutStatePacket { Json = JsonConvert.SerializeObject(view), Open = open, RequestId = requestId });
    }
}
