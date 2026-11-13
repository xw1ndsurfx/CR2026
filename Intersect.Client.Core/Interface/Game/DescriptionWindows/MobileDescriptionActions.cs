using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Interface.Game.DescriptionWindows;

internal static class MobileDescriptionActions
{
    internal static string? ItemCaption(ItemType type, bool equipped, bool quickCast) => type switch
    {
        ItemType.Equipment => ClientLanguages.Text(equipped ? "Unequip" : "Equip"),
        ItemType.Consumable or ItemType.Event => ClientLanguages.Text("Use"),
        ItemType.Bag => ClientLanguages.Text("Open"),
        ItemType.Spell => ClientLanguages.Text(quickCast ? "Cast spell" : "Learn spell"),
        _ => null,
    };

    private static void CloseCurrent()
    {
        Interface.GameUi.CloseMobileDescriptions();
    }

    internal static void ShowInventory(int slot, Base owner)
    {
        if (Globals.Me is not { } player || slot < 0 || slot >= player.Inventory.Length ||
            player.Inventory[slot] is not { Quantity: > 0, Descriptor: { } item } instance) return;
        var id = item.Id;
        var equipped = player.IsEquipped(slot);
        CloseCurrent();
        var window = Interface.GameUi.ItemDescriptionWindow!;
        window.Show(item, instance.Quantity, instance.ItemProperties);
        window.PinMobileAction(owner, ItemCaption(item.ItemType, equipped, item.QuickCast),
            () => ReferenceEquals(Globals.Me, player) && slot < player.Inventory.Length &&
                ReferenceEquals(player.Inventory[slot], instance) && instance.ItemId == id && instance.Quantity > 0 &&
                (item.ItemType != ItemType.Equipment || player.IsEquipped(slot) == equipped),
            () => player.TryUseItem(slot));
    }

    internal static void ShowEquipment(int equipmentSlot, Base owner)
    {
        if (Globals.Me is not { } player || equipmentSlot < 0 || equipmentSlot >= player.MyEquipment.Length) return;
        var slot = player.MyEquipment[equipmentSlot];
        if (slot < 0 || slot >= player.Inventory.Length || player.Inventory[slot]?.Descriptor is not { } item) return;
        var id = item.Id;
        CloseCurrent();
        var window = Interface.GameUi.ItemDescriptionWindow!;
        window.Show(item, 1, player.Inventory[slot].ItemProperties);
        window.PinMobileAction(owner, ClientLanguages.Text("Unequip"),
            () => ReferenceEquals(Globals.Me, player) && equipmentSlot < player.MyEquipment.Length &&
                player.MyEquipment[equipmentSlot] == slot && slot < player.Inventory.Length && player.Inventory[slot]?.ItemId == id,
            () => PacketSender.SendUnequipItem(equipmentSlot));
    }

    internal static void ShowSpell(int slot, Base owner)
    {
        if (Globals.Me is not { } player || slot < 0 || slot >= player.Spells.Length || player.Spells[slot].Id == Guid.Empty) return;
        var id = player.Spells[slot].Id;
        CloseCurrent();
        var window = Interface.GameUi.SpellDescriptionWindow!;
        window.Show(id);
        window.PinMobileAction(owner, ClientLanguages.Text("Cast spell"),
            () => ReferenceEquals(Globals.Me, player) && slot < player.Spells.Length && player.Spells[slot].Id == id,
            () => player.TryUseSpell(slot));
    }
}
