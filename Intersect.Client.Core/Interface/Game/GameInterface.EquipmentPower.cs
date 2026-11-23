using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;

namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private Button? _equipmentPowerButton;
    private Label? _equipmentPowerKey;
    private void UpdateEquipmentPower()
    {
        var player = Globals.Me;
        var slot = player?.MyEquipment.ElementAtOrDefault(Options.Instance.Equipment.WeaponSlot) ?? -1;
        var item = player != null && slot >= 0 && slot < player.Inventory.Length ? player.Inventory[slot] : null;
        var descriptor = item == null ? null : ItemDescriptor.Get(item.ItemId);
        var binding = descriptor?.EffectiveWeaponSpell(item?.ItemProperties);
        if (!CanShowWeaponSkills || binding is not { Trigger: WeaponSpellTrigger.Manual }) { _equipmentPowerButton?.Hide(); return; }
        if (_equipmentPowerButton == null)
        {
            _equipmentPowerButton = NavalManagementWindow.Button(GameCanvas, "EquippedWeaponSpell", "", 0, 0, 38, 38, UseEquipmentSpell);
            EquipmentPresentation.Icon(_equipmentPowerButton, 2, 3, 0, 32);
            _equipmentPowerKey = NavalManagementWindow.Text(_equipmentPowerButton, "EquipmentSpellKey", "", 0, 24, 38, 14, 9);
            _equipmentPowerKey.MouseInputEnabled = false;
        }
        _equipmentPowerButton.SetPosition(Math.Max(2, Hotbar.X - 44), Hotbar.Y);
        _equipmentPowerButton.TooltipText = EquipmentPresentation.T("Weapon spell") + ": " + Localization.ClientLanguages.ContentText(SpellDescriptor.Get(binding.SpellId)?.Name ?? "") + "\n" + string.Format(EquipmentPresentation.T("Shortcut: {0}. Change it in Settings > Controls."), EquipmentPresentation.EquipmentSpellKey());
        _equipmentPowerKey!.Text = EquipmentPresentation.EquipmentSpellKey() == EquipmentPresentation.T("Unassigned") ? "" : EquipmentPresentation.EquipmentSpellKey();
        _equipmentPowerButton.Show();
    }
}
