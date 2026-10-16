namespace Intersect.Editor.Forms.Editors;

internal static class MiniGameEditorTheme
{
    public static void Apply(Control root)
    {
        root.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        root.ForeColor = System.Drawing.Color.Gainsboro;
        if (root is Form form) { form.Font = new Font("Segoe UI", 9); form.MaximizeBox = true; }
        if (root is TextBoxBase or ListBox or ComboBox or NumericUpDown)
            root.BackColor = System.Drawing.Color.FromArgb(57, 60, 62);
        if (root is ListBox list) { list.IntegralHeight = false; list.BorderStyle = BorderStyle.FixedSingle; }
        if (root is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(95, 95, 98);
            button.BackColor = System.Drawing.Color.FromArgb(65, 65, 69);
            button.MinimumSize = new Size(0, 28);
        }
        if (root is TabControl tabs)
        {
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(140, 32);
            tabs.DrawItem += (_, e) =>
            {
                using var brush = new SolidBrush(e.Index == tabs.SelectedIndex ? System.Drawing.Color.FromArgb(76, 68, 54) : System.Drawing.Color.FromArgb(45, 45, 48));
                e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds, System.Drawing.Color.Gainsboro,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
        }
        foreach (Control child in root.Controls) Apply(child);
    }
}
