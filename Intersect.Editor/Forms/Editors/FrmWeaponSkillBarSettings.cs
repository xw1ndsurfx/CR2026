using System.Text.Json;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

internal sealed class FrmWeaponSkillBarSettings : Form
{
    internal WeaponSkillBarSettings Result { get; }
    private static string T(string text) => QualityOfLifeText.Get(text,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    internal FrmWeaponSkillBarSettings(WeaponSkillBarSettings settings)
    {
        Result = JsonSerializer.Deserialize<WeaponSkillBarSettings>(JsonSerializer.Serialize(settings))!;
        Text = T("Weapon skill quickbar"); Size = new Size(530, 360); MinimumSize = new Size(470, 330);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var enabled = new CheckBox { Name = "WeaponSkillBarEnabled", Text = T("Show weapon skill quickbar"),
            Checked = Result.Enabled, AutoSize = true };
        enabled.CheckedChanged += (_, _) => Result.Enabled = enabled.Checked;
        table.Controls.Add(enabled, 0, 0); table.SetColumnSpan(enabled, 2);
        void Number(string label, string name, int value, int min, int max, Action<int> changed, int row)
        {
            table.Controls.Add(new Label { Text = T(label), AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, row);
            var control = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill };
            control.ValueChanged += (_, _) => changed((int)control.Value);
            table.Controls.Add(control, 1, row);
        }
        Number("Slot size (pixels)", "WeaponSkillSlotSize", Result.SlotSize, 24, 64, value => Result.SlotSize = value, 1);
        Number("Slot spacing (pixels)", "WeaponSkillSlotGap", Result.Gap, 0, 12, value => Result.Gap = value, 2);
        Number("Maximum columns", "WeaponSkillColumns", Result.MaximumColumns, 1, 16, value => Result.MaximumColumns = value, 3);
        Number("Space above hotbar (pixels)", "WeaponSkillHotbarSpacing", Result.HotbarSpacing, 0, 48, value => Result.HotbarSpacing = value, 4);
        var note = new Label { Text = T("Choose each skill icon in the mastery talent tree. The bar is hidden aboard ships, in mini-games and while a window is open."),
            Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(470, 0) };
        table.Controls.Add(note, 0, 5); table.SetColumnSpan(note, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = T("Save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange([save, cancel]); Controls.Add(table); Controls.Add(footer);
        AcceptButton = save; CancelButton = cancel;
    }
}
