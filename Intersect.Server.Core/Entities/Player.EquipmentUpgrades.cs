using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Networking;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;

public sealed record EquipmentUpgradeTarget(int Slot, Guid ItemId, ItemProperties Properties, string Recipe);

public partial class Player
{
    internal bool EquipmentUpgradeSelectionMatches(CraftingRecipeDescriptor recipe, int slot, ItemProperties? expected) =>
        expected?.StatModifiers != null && slot >= 0 && slot < Items.Count && Items[slot] is { Quantity: 1 } item && item.ItemId == recipe.ItemId &&
        item.Properties.StatModifiers.SequenceEqual(expected.StatModifiers) && EquipmentEnhancement.Same(item.Properties.Enhancement, expected.Enhancement);

    internal string PrepareEquipmentUpgrade(CraftingRecipeDescriptor recipe, int slot, out EquipmentUpgradeTarget? target)
    {
        target = null;
        if (slot < 0 || slot >= Items.Count || Items[slot] is not { Quantity: 1 } item || item.ItemId != recipe.ItemId || item.Descriptor?.ItemType != ItemType.Equipment)
            return "Choose the equipment copy you want to improve.";
        var candidate = new EquipmentUpgradeTarget(slot, item.ItemId, new ItemProperties(item.Properties), JsonConvert.SerializeObject(recipe));
        var problem = PlanEquipmentUpgrade(recipe, candidate, out _, out _);
        if (problem.Length == 0) target = candidate;
        return problem;
    }

    internal string PlanEquipmentUpgrade(CraftingRecipeDescriptor recipe, EquipmentUpgradeTarget? target, out int[] quantities, out ItemProperties? result)
    {
        quantities = []; result = null;
        if (CombatTimer > Timing.Global.Milliseconds || Naval.NavalSystem.EquipmentChangeBlocked(this)) return "Equipment cannot be improved during combat.";
        if (IsDead || HasPendingDeath || IsCasting || Trading.Counterparty != null || InBank || HasBlockingEventHold)
            return "Finish your current action before improving equipment.";
        if (!Intersect.Server.Entities.Events.Conditions.MeetsConditionLists(recipe.CraftingRequirements, this, null))
            return "You do not meet this recipe's requirements.";
        if (target == null || target.Slot < 0 || target.Slot >= Items.Count || Items[target.Slot] is not { Quantity: 1 } item ||
            item.ItemId != target.ItemId || item.ItemId != recipe.ItemId ||
            !item.Properties.StatModifiers.SequenceEqual(target.Properties.StatModifiers) ||
            !EquipmentEnhancement.Same(item.Properties.Enhancement, target.Properties.Enhancement) ||
            target.Recipe != JsonConvert.SerializeObject(recipe)) return "The equipment or recipe changed. Choose it again.";
        var descriptor = item.Descriptor;
        if (recipe.EquipmentUpgrade is not {} upgrade || recipe.Campfire != null || recipe.Quantity != 1 || descriptor?.ItemType != ItemType.Equipment ||
            upgrade.TargetRarity < -1 || upgrade.TargetRarity >= Options.Instance.Items.RarityTiers.Count ||
            upgrade.Spell is {} spell && (descriptor.EquipmentSlot != Options.Instance.Equipment.WeaponSlot || SpellDescriptor.Get(spell.SpellId) == null ||
                !Enum.IsDefined(spell.Trigger) || spell.ChancePercent is < 1 or > 100) ||
            upgrade.EventId != Guid.Empty && (EventDescriptor.Get(upgrade.EventId)?.CommonEvent != true || upgrade.EventTrigger is < ItemEventTrigger.OnUse or > ItemEventTrigger.OnDamageReceived))
            return "This equipment upgrade recipe is invalid.";
        if (upgrade.TargetRarity >= 0 && upgrade.TargetRarity < descriptor.EffectiveRarity(item.Properties))
            return "This recipe cannot lower an item's rarity.";
        result = new ItemProperties(item.Properties);
        result.Enhancement ??= new();
        if (upgrade.TargetRarity >= 0) result.Enhancement.Rarity = upgrade.TargetRarity;
        if (upgrade.Spell != null) result.Enhancement.Spell = upgrade.Spell.Copy();
        if (upgrade.EventId != Guid.Empty) { result.Enhancement.EventId = upgrade.EventId; result.Enhancement.EventTrigger = upgrade.EventTrigger; }
        if ((upgrade.TargetRarity < 0 || descriptor.EffectiveRarity(item.Properties) == upgrade.TargetRarity) &&
            (upgrade.Spell == null || upgrade.Spell.SameAs(descriptor.EffectiveWeaponSpell(item.Properties))) &&
            (upgrade.EventId == Guid.Empty || item.Properties.Enhancement is {} previous && previous.EventId == upgrade.EventId && previous.EventTrigger == upgrade.EventTrigger))
            return "This equipment already has the requested improvements.";
        quantities = Items.Select(i => i.Quantity).ToArray();
        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.ItemId == Guid.Empty || ingredient.Quantity < 1) return "This equipment upgrade recipe is invalid.";
            var remaining = ingredient.Quantity;
            for (var i = 0; i < Items.Count && remaining > 0; i++)
            {
                if (i == target.Slot || Equipment.Contains(i) || Items[i].ItemId != ingredient.ItemId) continue;
                var take = Math.Min(remaining, quantities[i]); quantities[i] -= take; remaining -= take;
            }
            if (remaining > 0) return "You do not have the required upgrade materials.";
        }
        return "";
    }

    // EntityLock + the account save gate are held by the caller. The chosen item is never destroyed on failure.
    internal string CommitEquipmentUpgrade(CraftingRecipeDescriptor recipe, EquipmentUpgradeTarget target, bool failed, bool loseMaterials, Func<bool> save)
    {
        var error = PlanEquipmentUpgrade(recipe, target, out var quantities, out var result);
        if (error.Length > 0) return error;
        if (failed && !loseMaterials) return "Upgrade failed. Your equipment and materials were kept.";
        var before = Items.Select(i => i.Clone()).ToArray(); var committed = false;
        try
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (quantities[i] == Items[i].Quantity) continue;
                if (quantities[i] == 0) Items[i].Set(Item.None); else Items[i].Quantity = quantities[i];
            }
            if (!failed) Items[target.Slot].Properties = result!;
            committed = save();
            return committed ? failed ? "Upgrade failed. Materials were consumed; your equipment was kept." : "Equipment improved." : "Unable to save the upgrade. Your items were restored.";
        }
        finally { if (!committed) for (var i = 0; i < before.Length; i++) Items[i].Set(before[i]); }
    }

    private void CompleteEquipmentUpgrade(CraftingRecipeDescriptor recipe)
    {
        if (CraftingState?.UpgradeTarget is not {} target || User == null) { CraftingState = null; return; }
        string result;
        try
        {
            lock (User.PokerSaveGate)
                result = CommitEquipmentUpgrade(recipe, target, Random.Shared.Next(100) < recipe.FailureChance,
                    Random.Shared.Next(100) < recipe.ItemLossChance, () => User.Save(force: true) == UserSaveResult.Completed);
        }
        catch (Exception error)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(error, "Equipment upgrade save failed for {PlayerId}", Id);
            result = "Unable to save the upgrade. Your items were restored.";
        }
        CraftingState = null;
        if (result == "Equipment improved.")
        {
            if (recipe.Event != null) EnqueueStartCommonEvent(recipe.Event);
            ProcessEquipmentUpdated(true);
        }
        PacketSender.SendInventory(this);
        SendPacket(new Intersect.Network.Packets.EquipmentLoadouts.EquipmentFeedbackPacket { Cue = result == "Equipment improved." ? "upgrade" : "error" });
        EquipmentUpgradeNotice(result);
    }

    internal void EquipmentUpgradeNotice(string message) => PacketSender.SendChatMsg(this,
        Intersect.Framework.Core.EquipmentLoadouts.EquipmentLoadoutText.Get(message, Client?.LanguageCode ?? "en"), ChatMessageType.Crafting);
}
