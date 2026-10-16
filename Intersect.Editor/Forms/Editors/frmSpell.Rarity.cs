using DarkUI.Controls;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmSpell
{
    private readonly DarkGroupBox _spellRarityGroup = new() { Text = "Rarity", Width = 270, Height = 82 };
    private readonly DarkComboBox _spellRarity = new() { Left = 12, Top = 30, Width = 246, DropDownStyle = ComboBoxStyle.DropDownList };
    private void InitializeSpellRarity()
    {
        _spellRarityGroup.Controls.Add(_spellRarity);
        foreach (var name in Options.Instance.Items.RarityTiers)
            _spellRarity.Items.Add(Intersect.Editor.Localization.Strings.ItemEditor.rarity.GetValueOrDefault(name, name));
        _spellRarity.SelectedIndexChanged += (_, _) =>
        {
            if (mEditorItem != null && _spellRarity.SelectedIndex >= 0) mEditorItem.Rarity = _spellRarity.SelectedIndex;
        };
    }
}
