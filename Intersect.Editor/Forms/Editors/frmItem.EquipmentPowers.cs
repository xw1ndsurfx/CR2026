using System.Windows.Forms;
using DarkUI.Controls;

namespace Intersect.Editor.Forms.Editors;
public partial class FrmItem
{
    private DarkGroupBox BuildEquipmentPowerGroup()
    {
        var group = new DarkGroupBox { Width = 610, Height = 155, Text = EquipmentEffectsDialog.T("Weapon spell") };
        group.Controls.Add(new Label { Left = 18, Top = 24, Width = 570, Height = 48, Text = EquipmentEffectsDialog.T("Only weapons support a spell. The usual mana costs, requirements and cooldowns apply."), ForeColor = System.Drawing.Color.Gainsboro });
        var configure = new DarkButton { Left = 18, Top = 85, Width = 350, Height = 34, Text = EquipmentEffectsDialog.T("Configure weapon spell") };
        configure.Click += (_, _) =>
        {
            if (mEditorItem == null) return;
            using var dialog = new EquipmentEffectsDialog(mEditorItem, mEditorItem.Presentation.WeaponSpell, null, false);
            if (dialog.ShowDialog(this) == DialogResult.OK) mEditorItem.Presentation.WeaponSpell = dialog.Binding;
        };
        group.Controls.Add(configure); return group;
    }
}
