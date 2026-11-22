using Intersect.Framework.Core.RoyalSeasons;
namespace Intersect.Editor.Forms.Editors;
internal static class RoyalSeasonEditorControls
{
    internal sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    internal static string T(string text) => RoyalSeasonText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal static TableLayoutPanel Grid() { var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) }; grid.ColumnStyles.Add(new(SizeType.Percent, 42)); grid.ColumnStyles.Add(new(SizeType.Percent, 58)); return grid; }
    internal static void Row(TableLayoutPanel grid, string label, Control input)
    {
        var row = grid.RowCount++; grid.RowStyles.Add(new(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = T(label), AutoSize = true, MaximumSize = new Size(300, 0), Margin = new Padding(4, 8, 8, 8) }, 0, row);
        input.Name = label; input.AccessibleName = T(label);
        input.Dock = DockStyle.Top; input.Margin = new Padding(4, 5, 4, 5); grid.Controls.Add(input, 1, row);
    }
    internal static void Text(TableLayoutPanel grid, string label, string value, int max, Action<string> change)
    { var box = new TextBox { Text = value, MaxLength = max, Multiline = max > 100, Height = max > 100 ? 70 : 28 }; box.TextChanged += (_, _) => change(box.Text); Row(grid, label, box); }
    internal static void Check(TableLayoutPanel grid, string label, bool value, Action<bool> change)
    { var box = new CheckBox { Checked = value, AutoSize = true }; box.CheckedChanged += (_, _) => change(box.Checked); Row(grid, label, box); }
    internal static void Number(TableLayoutPanel grid, string label, int value, int min, int max, Action<int> change)
    { var box = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) }; box.ValueChanged += (_, _) => change((int)box.Value); Row(grid, label, box); }
    internal static void Select(TableLayoutPanel grid, string label, Guid value, IEnumerable<Choice> choices, Action<Guid> change)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; box.Items.Add(new Choice(Guid.Empty, T("Any / none")));
        box.Items.AddRange(choices.OrderBy(c => c.Name).Cast<object>().ToArray());
        if (value != Guid.Empty && !box.Items.Cast<Choice>().Any(c => c.Id == value)) box.Items.Add(new Choice(value, T("Missing reference") + " " + value));
        box.SelectedItem = box.Items.Cast<Choice>().First(c => c.Id == value);
        box.SelectedIndexChanged += (_, _) => { if (box.SelectedItem is Choice selected) change(selected.Id); }; Row(grid, label, box);
    }
    internal static Button ActionButton(string caption, Action action)
    { var button = new Button { Text = T(caption), AutoSize = true }; button.Click += (_, _) => action(); return button; }
}
