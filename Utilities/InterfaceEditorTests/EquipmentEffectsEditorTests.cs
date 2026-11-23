using System.Reflection;
using System.Globalization;
using Intersect;
using Intersect.Enums;
using Intersect.Framework.Core.EquipmentLoadouts;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.GameObjects;

internal static class EquipmentEffectsEditorTests
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var item = new ItemDescriptor(Guid.NewGuid()) { ItemType = ItemType.Equipment, EquipmentSlot = Options.Instance.Equipment.WeaponSlot, Rarity = 1 };
        var spell = new SpellDescriptor(Guid.NewGuid()) { Name = "Equipment test spell" }; SpellDescriptor.Lookup.Set(spell.Id, spell);
        var evt = new EventDescriptor(Guid.NewGuid()) { Name = "Equipment test event", CommonEvent = true }; EventDescriptor.Lookup.Set(evt.Id, evt);
        var type = typeof(Intersect.Editor.Forms.Editors.FrmItem).Assembly.GetType("Intersect.Editor.Forms.Editors.EquipmentEffectsDialog", true)!;
        var language = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var locale in new[] { "en", "fr", "es" }) foreach (var crafting in new[] { false, true })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
                var original = new EquipmentUpgradeRecipe { TargetRarity = 2, Spell = new() { SpellId = spell.Id }, EventId = evt.Id, EventTrigger = ItemEventTrigger.OnEquip };
                using var form = (Form)Activator.CreateInstance(type, Hidden, null, [item, original.Spell, original, crafting], null)!;
                form.CreateControl(); form.PerformLayout();
                ComboBox Combo(string name) => (ComboBox)form.Controls[name]!;
                Check(form.Text == EquipmentLoadoutText.Get(crafting ? "Equipment forge" : "Weapon spell", locale), "Dialog title not localized");
                Combo("EquipmentSpellTrigger").SelectedIndex = 1;
                ((NumericUpDown)form.Controls["EquipmentSpellChance"]!).Value = 42;
                Check(original.Spell.Trigger == WeaponSpellTrigger.Manual, "Dialog mutates source before save");
                if (crafting) { Combo("EquipmentUpgradeRarity").SelectedIndex = 4; Combo("EquipmentEventTrigger").SelectedIndex = 4; }
                type.GetMethod("SaveEffects", Hidden)!.Invoke(form, []);
                Check(form.DialogResult == DialogResult.OK, "Valid effects were refused");
                var binding = (WeaponSpellBinding)type.GetProperty("Binding", Hidden)!.GetValue(form)!;
                Check(binding.Trigger == WeaponSpellTrigger.OnHit && binding.ChancePercent == 42 && binding.SpellId == spell.Id, "Wrong spell binding");
                if (crafting)
                {
                    var upgrade = (EquipmentUpgradeRecipe)type.GetProperty("Upgrade", Hidden)!.GetValue(form)!;
                    Check(upgrade.TargetRarity == 3 && upgrade.EventId == evt.Id && upgrade.EventTrigger == ItemEventTrigger.OnDamageReceived, "Wrong per-copy upgrade");
                }
                Check(form.Controls["SaveEquipmentEffects"]!.Bottom <= form.ClientSize.Height, "Save button outside viewport");
                System.Console.WriteLine($"PASS equipment editor / {locale} / crafting={crafting}: selection, trigger, chance, rarity, event, no pre-save mutation");
            }
        }
        finally { SpellDescriptor.Lookup.Delete(spell); EventDescriptor.Lookup.Delete(evt); CultureInfo.CurrentUICulture = language; }
    }
}
