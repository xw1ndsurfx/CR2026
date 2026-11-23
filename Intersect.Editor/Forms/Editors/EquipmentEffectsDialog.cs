using System.Globalization;
using System.Windows.Forms;
using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

internal sealed class EquipmentEffectsDialog : DarkForm
{
    internal static string T(string text) => EquipmentLoadoutText.Get(text, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly CheckBox _enabled;
    private readonly ComboBox _spell, _trigger, _rarity, _event, _eventTrigger;
    private readonly NumericUpDown _chance;
    private readonly bool _upgrade;
    private readonly ItemDescriptor? _item;
    internal WeaponSpellBinding? Binding { get; private set; }
    internal EquipmentUpgradeRecipe? Upgrade { get; private set; }

    internal EquipmentEffectsDialog(ItemDescriptor? item, WeaponSpellBinding? binding, EquipmentUpgradeRecipe? upgrade, bool crafting)
    {
        _upgrade = crafting; _item = item; Binding = binding?.Copy(); Upgrade = upgrade;
        Text = T(crafting ? "Equipment forge" : "Weapon spell"); StartPosition = FormStartPosition.CenterParent;
        ClientSize = new(650, crafting ? 540 : 380); MinimumSize = new(600, 380); MaximizeBox = false; AutoScroll = true;
        _enabled = new CheckBox { Name = "EnableEquipmentEffects", Text = T(crafting ? "Improve an existing equipment copy" : "Enable weapon spell"), Left = 20, Top = 18, Width = 600, Checked = crafting ? upgrade != null : binding != null };
        Controls.Add(_enabled);
        var help = new Label { Text = T(crafting ? "The result item identifies the equipment to improve. Ingredients are additional materials. Other copies are unchanged." : "Only weapons support a spell. The usual mana costs, requirements and cooldowns apply."), Left = 20, Top = 50, Width = 600, Height = 52, ForeColor = System.Drawing.Color.Gainsboro };
        Controls.Add(help);
        ComboBox Combo(string title, string name, int y)
        {
            Controls.Add(new Label { Text = T(title), Left = 20, Top = y + 4, Width = 240, Height = 30, ForeColor = System.Drawing.Color.Gainsboro });
            var combo = new ComboBox { Name = name, Left = 265, Top = y, Width = 350, DropDownStyle = ComboBoxStyle.DropDownList }; Controls.Add(combo); return combo;
        }
        _spell = Combo("Weapon spell", "EquipmentSpell", 108); _spell.Items.Add(new Choice(Guid.Empty, T("None / keep existing")));
        foreach (var spell in SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>().OrderBy(s => s.Name)) _spell.Items.Add(new Choice(spell.Id, spell.Name));
        _spell.SelectedItem = _spell.Items.Cast<Choice>().FirstOrDefault(c => c.Id == (crafting ? upgrade?.Spell?.SpellId : binding?.SpellId)) ?? _spell.Items[0];
        _trigger = Combo("Activation", "EquipmentSpellTrigger", 148); _trigger.Items.AddRange([T("Manual"), T("Automatic on hit")]);
        _trigger.SelectedIndex = (int)((crafting ? upgrade?.Spell?.Trigger : binding?.Trigger) ?? WeaponSpellTrigger.Manual);
        Controls.Add(new Label { Text = T("Chance on hit (%)"), Left = 20, Top = 192, Width = 235, ForeColor = System.Drawing.Color.Gainsboro });
        _chance = new NumericUpDown { Name = "EquipmentSpellChance", Left = 265, Top = 188, Width = 140, Minimum = 1, Maximum = 100, Value = Math.Clamp((crafting ? upgrade?.Spell?.ChancePercent : binding?.ChancePercent) ?? 100, 1, 100), Enabled = _trigger.SelectedIndex == 1 }; Controls.Add(_chance);
        _trigger.SelectedIndexChanged += (_, _) => _chance.Enabled = _trigger.SelectedIndex == 1;
        _rarity = Combo("Target rarity", "EquipmentUpgradeRarity", 232); _rarity.Items.Add(T("None / keep existing"));
        foreach (var rarity in Options.Instance.Items.RarityTiers) _rarity.Items.Add(Localization.Strings.ItemEditor.rarity.GetValueOrDefault(rarity, rarity));
        _rarity.SelectedIndex = Math.Clamp((upgrade?.TargetRarity ?? -1) + 1, 0, _rarity.Items.Count - 1);
        _event = Combo("Bound common event", "EquipmentUpgradeEvent", 272); _event.Items.Add(new Choice(Guid.Empty, T("None / keep existing")));
        foreach (var evt in EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent).OrderBy(e => e.Name)) _event.Items.Add(new Choice(evt.Id, evt.Name));
        _event.SelectedItem = _event.Items.Cast<Choice>().FirstOrDefault(c => c.Id == upgrade?.EventId) ?? _event.Items[0];
        _eventTrigger = Combo("Event trigger", "EquipmentEventTrigger", 312);
        foreach (var trigger in Enum.GetValues<ItemEventTrigger>().Where(t => t >= ItemEventTrigger.OnUse)) _eventTrigger.Items.Add(T(trigger.ToString()));
        _eventTrigger.SelectedIndex = Math.Clamp((int)(upgrade?.EventTrigger ?? ItemEventTrigger.OnEquip) - (int)ItemEventTrigger.OnUse, 0, _eventTrigger.Items.Count - 1);
        if (!crafting) foreach (Control control in Controls) if (control.Top >= 232) control.Visible = false;
        var note = new Label { Text = T(crafting ? "The bound event runs on this item when triggered. The recipe event still runs after a successful craft." : "Manual: assign Weapon spell in Settings > Controls. Automatic: each successful hit rolls its chance."), Left = 20, Top = crafting ? 360 : 237, Width = 600, Height = 60, ForeColor = System.Drawing.Color.Gainsboro }; Controls.Add(note);
        var save = new Button { Name = "SaveEquipmentEffects", Text = T("Save"), Left = 390, Top = crafting ? 460 : 315, Width = 105, Height = 32 };
        var cancel = new Button { Text = T("Close"), Left = 510, Top = save.Top, Width = 105, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => SaveEffects(); Controls.AddRange([save, cancel]); CancelButton = cancel;
    }

    private void SaveEffects()
    {
        if (!_enabled.Checked) { Binding = null; Upgrade = null; DialogResult = DialogResult.OK; return; }
        var spellId = ((Choice)_spell.SelectedItem!).Id;
        if (_item?.ItemType != ItemType.Equipment || (spellId != Guid.Empty || !_upgrade) && _item.EquipmentSlot != Options.Instance.Equipment.WeaponSlot)
        { MessageBox.Show(this, T("Choose equipment as the recipe result; spells require a weapon.")); return; }
        if (!_upgrade && spellId == Guid.Empty) { MessageBox.Show(this, T("Choose a spell.")); return; }
        Binding = spellId == Guid.Empty ? null : new WeaponSpellBinding { SpellId = spellId, Trigger = (WeaponSpellTrigger)_trigger.SelectedIndex, ChancePercent = (int)_chance.Value };
        if (_upgrade)
        {
            Upgrade = new() { TargetRarity = _rarity.SelectedIndex - 1, Spell = Binding, EventId = ((Choice)_event.SelectedItem!).Id, EventTrigger = (ItemEventTrigger)(_eventTrigger.SelectedIndex + (int)ItemEventTrigger.OnUse) };
            if (Upgrade.TargetRarity < 0 && Binding == null && Upgrade.EventId == Guid.Empty) { MessageBox.Show(this, T("Choose at least one improvement.")); return; }
            if (Upgrade.TargetRarity >= 0 && Upgrade.TargetRarity < _item.Rarity) { MessageBox.Show(this, T("This recipe cannot lower an item's rarity.")); return; }
        }
        DialogResult = DialogResult.OK;
    }
}
