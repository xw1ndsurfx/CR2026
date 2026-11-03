using System.Windows.Forms;
using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;
public partial class FrmCrafts
{
    private Button _campfireResult = null!;
    private static string CampfireText(string text) => QualityOfLifeText.Get(text,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private void InitializeCampfireResult()
    {
        // A separate page keeps the existing recipe and ingredient controls at their original positions.
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var recipe = new TabPage(CampfireText("Recipe")) { AutoScroll = true };
        var fire = new TabPage(CampfireText("Campfire"));
        var groups = pnlContainer.Controls.Cast<Control>().ToArray();
        recipe.Controls.AddRange(groups);
        tabs.TabPages.AddRange([recipe, fire]);
        pnlContainer.Controls.Add(tabs);
        _campfireResult = new Button { Text = CampfireText("Configure campfire result"), Left = 20, Top = 24, Width = 350, Height = 38 };
        _campfireResult.Click += (_, _) => ConfigureCampfireResult();
        fire.Controls.Add(_campfireResult);
    }
    private void UpdateCampfireResultButton()
    {
        var placed = mEditorItem?.Campfire != null;
        cmbResult.Enabled = nudCraftQuantity.Enabled = !placed;
        _campfireResult.Text = CampfireText(placed ? "Campfire result configured" : "Configure campfire result");
    }
    private sealed record AnimationChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private void ConfigureCampfireResult()
    {
        if (mEditorItem == null) return;
        using var dialog = new DarkForm { Text = CampfireText("Campfire"), Width = 600, Height = 375,
            StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var enabled = new CheckBox { Text = CampfireText("Create a campfire instead of an item"), Left = 20, Top = 20, Width = 550,
            Checked = mEditorItem.Campfire != null, ForeColor = System.Drawing.Color.Gainsboro };
        dialog.Controls.Add(enabled);
        ComboBox Animation(string title, int y, Guid selected)
        {
            dialog.Controls.Add(new Label { Text = CampfireText(title), Left = 20, Top = y+5, Width = 200, ForeColor = System.Drawing.Color.Gainsboro });
            var combo = new ComboBox { Left = 230, Top = y, Width = 325, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.Add(new AnimationChoice(Guid.Empty, CampfireText("None")));
            foreach(var animation in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name))
                Intersect.Editor.Forms.Styling.SearchableChoices.SetDescriptor(combo,
                    combo.Items.Add(new AnimationChoice(animation.Id, animation.Name)), animation);
            combo.SelectedItem = combo.Items.Cast<AnimationChoice>().FirstOrDefault(a => a.Id == selected) ?? combo.Items[0];
            dialog.Controls.Add(combo); return combo;
        }
        var source = mEditorItem.Campfire ?? new CampfireRecipeResult();
        var flames = Animation("Fire animation", 65, source.FireAnimationId);
        var ashes = Animation("Ashes animation", 105, source.AshesAnimationId);
        NumericUpDown Seconds(string title, int y, int value, int min, int max)
        {
            dialog.Controls.Add(new Label { Text = CampfireText(title), Left = 20, Top = y+5, Width = 200, ForeColor = System.Drawing.Color.Gainsboro });
            var input = new NumericUpDown { Left = 230, Top = y, Width = 160, Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) };
            dialog.Controls.Add(input); return input;
        }
        var burning = Seconds("Fire duration (seconds)", 150, source.FireSeconds, 10, 3600);
        var fading = Seconds("Ashes duration (seconds)", 190, source.AshesSeconds, 1, 600);
        var save = new Button { Text = CampfireText("Save"), Left = 355, Top = 270, Width = 95, Height = 30 };
        var cancel = new Button { Text = CampfireText("Cancel"), Left = 460, Top = 270, Width = 95, Height = 30, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            var result = new CampfireRecipeResult { FireAnimationId = ((AnimationChoice)flames.SelectedItem).Id,
                AshesAnimationId = ((AnimationChoice)ashes.SelectedItem).Id, FireSeconds = (int)burning.Value, AshesSeconds = (int)fading.Value };
            if (enabled.Checked && !result.IsValid)
            { MessageBox.Show(dialog, CampfireText("Choose both the fire and ashes animations.")); return; }
            mEditorItem.Campfire = enabled.Checked ? result : null;
            UpdateCampfireResultButton(); dialog.DialogResult = DialogResult.OK;
        };
        dialog.Controls.AddRange([save, cancel]); dialog.CancelButton = cancel;
        dialog.ShowDialog(this);
    }
}
