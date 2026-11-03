using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.GameObjects;
namespace Intersect.Editor.Forms.Editors;
public sealed class FrmWeaponAmmunition : Form
{
    public WeaponAmmunitionRule[] Result => _rules.Values.Where(r => r.ProjectileIds.Length > 0).ToArray();
    private readonly Dictionary<Guid, WeaponAmmunitionRule> _rules;
    private readonly ListBox _weapons = new() { Dock = DockStyle.Fill, DisplayMember = "Name" };
    private readonly CheckedListBox _projectiles = new() { Dock = DockStyle.Fill, DisplayMember = "Name", CheckOnClick = true };
    private bool _refreshing;
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmWeaponAmmunition(WeaponAmmunitionRule[] rules)
    {
        _rules = JsonSerializer.Deserialize<WeaponAmmunitionRule[]>(JsonSerializer.Serialize(rules))!.ToDictionary(r => r.WeaponId);
        Text = T("Ammunition compatibility"); Size = new(900, 600); MinimumSize = new(760, 460);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(860, 480), SplitterDistance = 280 };
        split.Panel1.Controls.Add(_weapons); split.Panel2.Controls.Add(_projectiles); Controls.Add(split);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 };
        var save = new Button { Text = T("Save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        tools.Controls.Add(save); tools.Controls.Add(cancel); Controls.Add(tools);
        Controls.Add(new Label { Dock = DockStyle.Top, Height = 46, Text = T("Select existing alternate projectiles. The weapon's original ammunition remains available; effects and quantities come from each projectile.") });
        _weapons.Items.AddRange(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Equipment &&
            i.EquipmentSlot == Intersect.Options.Instance.Equipment.WeaponSlot && i.Projectile?.AmmoItemId != Guid.Empty && i.Projectile != null).OrderBy(i => i.Name).ToArray());
        _weapons.SelectedIndexChanged += (_, _) => {
            _refreshing = true; _projectiles.Items.Clear();
            if (_weapons.SelectedItem is ItemDescriptor weapon)
            {
                foreach (var projectile in ProjectileDescriptor.Lookup.Values.OfType<ProjectileDescriptor>().Where(p => p.Id != weapon.ProjectileId && p.AmmoRequired > 0 &&
                    ItemDescriptor.Get(p.AmmoItemId) is { Stackable: true } ammo && ammo.ItemType is ItemType.Ammo or ItemType.None or ItemType.Crafting).OrderBy(p => p.Name))
                    _projectiles.Items.Add(projectile, _rules.GetValueOrDefault(weapon.Id)?.ProjectileIds.Contains(projectile.Id) == true);
            }
            _refreshing = false;
        };
        _projectiles.ItemCheck += (_, e) => {
            if (_refreshing || _weapons.SelectedItem is not ItemDescriptor weapon || _projectiles.Items[e.Index] is not ProjectileDescriptor projectile) return;
            if (!_rules.TryGetValue(weapon.Id, out var rule)) _rules[weapon.Id] = rule = new() { WeaponId = weapon.Id };
            rule.ProjectileIds = e.NewValue == CheckState.Checked ? rule.ProjectileIds.Append(projectile.Id).Distinct().ToArray() : rule.ProjectileIds.Where(id => id != projectile.Id).ToArray();
        };
        if (_weapons.Items.Count > 0) _weapons.SelectedIndex = 0;
    }
}
