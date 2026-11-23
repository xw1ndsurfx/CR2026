using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors;
public partial class FrmCrafts
{
    private void InitializeEquipmentUpgrade(TabControl tabs)
    {
        var page = new TabPage(EquipmentEffectsDialog.T("Equipment forge")) { AutoScroll = true };
        var configure = new Button { Left = 20, Top = 24, Width = 370, Height = 38, Text = EquipmentEffectsDialog.T("Configure equipment improvement") };
        configure.Click += (_, _) =>
        {
            if (mEditorItem == null) return;
            using var dialog = new EquipmentEffectsDialog(ItemDescriptor.Get(mEditorItem.ItemId), null, mEditorItem.EquipmentUpgrade, true);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            mEditorItem.EquipmentUpgrade = dialog.Upgrade;
            if (dialog.Upgrade != null) { mEditorItem.Quantity = 1; mEditorItem.Campfire = null; }
            UpdateEditor();
        };
        page.Controls.Add(configure);
        page.Controls.Add(new Label { Left = 20, Top = 85, Width = 500, Height = 85, Text = EquipmentEffectsDialog.T("The result item identifies the equipment to improve. Ingredients are additional materials. Other copies are unchanged."), ForeColor = System.Drawing.Color.Gainsboro });
        tabs.TabPages.Add(page);
    }
}
