using System.Runtime.CompilerServices;
using WeifenLuo.WinFormsUI.Docking;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Styling;

internal static class EditorTheme
{
    internal static readonly DrawingColor Background = DrawingColor.FromArgb(15, 19, 27);
    internal static readonly DrawingColor Surface = DrawingColor.FromArgb(22, 29, 39);
    internal static readonly DrawingColor Input = DrawingColor.FromArgb(28, 37, 49);
    internal static readonly DrawingColor Border = DrawingColor.FromArgb(49, 64, 80);
    internal static readonly DrawingColor Text = DrawingColor.FromArgb(231, 238, 245);
    internal static readonly DrawingColor Muted = DrawingColor.FromArgb(162, 179, 198);
    internal static readonly DrawingColor Accent = DrawingColor.FromArgb(153, 211, 62);
    internal static readonly DrawingColor Primary = DrawingColor.FromArgb(69, 111, 35);
    internal static readonly DrawingColor Selection = DrawingColor.FromArgb(40, 65, 43);
    internal static readonly Font BodyFont = new("Segoe UI", 9f);
    private static readonly ConditionalWeakTable<Control, object> Watched = new();
    private static bool _installed;

    internal static void Install()
    {
        if (_installed) return;
        _installed = true;
        // Legacy dialogs are created from many independent editors. Apply once,
        // after construction; no timer or per-frame repaint/style traversal.
        Application.Idle += (_, _) =>
        {
            foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
                if (form is not FrmLogin and not FrmAbout and not FrmUpdate && !Watched.TryGetValue(form, out _))
                    Apply(form);
        };
    }

    internal static void Apply(Control root)
    {
        if (root is DockPanel or MdiClient) return; // Docking chrome owns its own palette and native child layout.
        if (root is ToolStrip warning && warning.BackColor.R > 80 && warning.BackColor.R > warning.BackColor.B * 2) return;
        if (root is Panel or Button && root.Controls.Count == 0 && root.Name.Contains("Color", StringComparison.OrdinalIgnoreCase)) return;
        if (root is PictureBox || root.IsDisposed) return; // Keep map canvases and authored color swatches intact.
        var first = !Watched.TryGetValue(root, out _);
        if (first)
        {
            Watched.Add(root, new object());
            if (root is not PropertyGrid)
                root.ControlAdded += (_, args) => { if (args.Control != null) Apply(args.Control); };
        }
        root.SuspendLayout();
        try
        {
            root.BackColor = root is Form ? Background : Surface;
            var foreground = root.ForeColor;
            root.ForeColor = foreground.R > 100 && foreground.R > foreground.G * 1.5 && foreground.R > foreground.B * 1.5
                ? DrawingColor.FromArgb(245, 126, 126) : foreground.R > 150 && foreground.R > foreground.G && foreground.G > foreground.B * 1.5
                ? DrawingColor.FromArgb(241, 192, 100) : Text;
            if (root.Font.FontFamily.Name is "Microsoft Sans Serif" or "Tahoma" || root is Form)
                root.Font = BodyFont;
            switch (root)
            {
                case ToolStrip strip:
                    strip.Renderer = new EditorStripRenderer();
                    strip.BackColor = Background;
                    strip.Font = BodyFont;
                    strip.GripStyle = ToolStripGripStyle.Hidden;
                    foreach (ToolStripItem item in strip.Items) StyleItem(item);
                    if (first) strip.ItemAdded += (_, args) => StyleItem(args.Item);
                    break;
                case PropertyGrid grid:
                    grid.ViewBackColor = Input; grid.ViewForeColor = Text; grid.ViewBorderColor = Border;
                    grid.CategoryForeColor = Accent; grid.CategorySplitterColor = Border; grid.LineColor = Border;
                    grid.HelpBackColor = Surface; grid.HelpForeColor = Muted; grid.HelpBorderColor = Border;
                    grid.CommandsBackColor = Surface; grid.CommandsForeColor = Text; grid.CommandsBorderColor = Border;
                    grid.SelectedItemWithFocusBackColor = Selection; grid.SelectedItemWithFocusForeColor = Text;
                    grid.DisabledItemForeColor = Muted;
                    break;
                case TreeView tree:
                    tree.BackColor = Input; tree.ForeColor = Text; tree.BorderStyle = BorderStyle.None;
                    tree.LineColor = Border; tree.ItemHeight = Math.Max(tree.ItemHeight, 22); tree.HideSelection = false;
                    tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
                    if (first) tree.DrawNode += DrawTreeNode;
                    break;
                case DataGridView grid:
                    grid.BackgroundColor = Input; grid.BorderStyle = BorderStyle.None; grid.GridColor = Border;
                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
                    grid.RowHeadersDefaultCellStyle.BackColor = Surface; grid.RowHeadersDefaultCellStyle.ForeColor = Muted;
                    grid.DefaultCellStyle.BackColor = Input; grid.DefaultCellStyle.ForeColor = Text;
                    grid.DefaultCellStyle.SelectionBackColor = Selection; grid.DefaultCellStyle.SelectionForeColor = Text;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = DrawingColor.FromArgb(25, 33, 44);
                    break;
                case TextBoxBase or ListBox or ComboBox or NumericUpDown:
                    root.BackColor = Input;
                    if (root is ListBox list) { list.IntegralHeight = false; list.BorderStyle = BorderStyle.FixedSingle; }
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = Selection; button.FlatAppearance.MouseDownBackColor = Primary;
                    button.BackColor = Input; button.UseVisualStyleBackColor = false;
                    break;
                case TabControl tabs:
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.ItemSize = new Size(140, 32);
                    if (first) tabs.DrawItem += (_, args) =>
                    {
                        var active = args.Index == tabs.SelectedIndex;
                        using var fill = new SolidBrush(active ? Selection : Surface);
                        args.Graphics.FillRectangle(fill, args.Bounds);
                        if (active) { using var line = new Pen(Accent, 2); args.Graphics.DrawLine(line, args.Bounds.Left + 4, args.Bounds.Bottom - 2, args.Bounds.Right - 4, args.Bounds.Bottom - 2); }
                        TextRenderer.DrawText(args.Graphics, tabs.TabPages[args.Index].Text, tabs.Font, args.Bounds, active ? Text : Muted,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    };
                    break;
            }
            if (root is not PropertyGrid)
                foreach (Control child in root.Controls.Cast<Control>().ToArray()) Apply(child);
            if (root.ContextMenuStrip != null) Apply(root.ContextMenuStrip);
        }
        finally { root.ResumeLayout(); }
    }

    private static void DrawTreeNode(object? sender, DrawTreeNodeEventArgs args)
    {
        if (args.Node == null) return;
        var selected = (args.State & TreeNodeStates.Selected) != 0;
        using var fill = new SolidBrush(selected ? Selection : Input);
        args.Graphics.FillRectangle(fill, args.Bounds);
        TextRenderer.DrawText(args.Graphics, args.Node.Text, args.Node.NodeFont ?? ((TreeView)sender!).Font, args.Bounds,
            selected || args.Node.ForeColor.IsEmpty ? Text : args.Node.ForeColor, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
    }

    private static void OnDropDownOpening(object? sender, EventArgs args)
    {
        if (sender is ToolStripDropDownItem item) Apply(item.DropDown);
    }

    private static void StyleItem(ToolStripItem item)
    {
        item.ForeColor = Text; item.BackColor = Background; item.Font = BodyFont;
        if (item is ToolStripStatusLabel status)
        { status.LinkColor = Accent; status.ActiveLinkColor = Text; status.VisitedLinkColor = Muted; }
        if (item is not ToolStripDropDownItem menu) return;
        menu.DropDownOpening -= OnDropDownOpening;
        menu.DropDownOpening += OnDropDownOpening;
        if (menu.HasDropDownItems) Apply(menu.DropDown);
    }
}
