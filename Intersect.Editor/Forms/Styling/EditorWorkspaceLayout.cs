namespace Intersect.Editor.Forms.Styling;

internal static class EditorWorkspaceLayout
{
    internal static void Apply(Form window, Control navigation, Control content, Control save, Control cancel)
    {
        window.SuspendLayout();
        window.AutoSize = false;
        window.FormBorderStyle = FormBorderStyle.Sizable;
        window.MaximizeBox = true;
        window.MaximumSize = Size.Empty;
        window.MinimumSize = new Size(800, 580);
        window.ClientSize = new Size(1100, 730);
        var footer = new FlowLayoutPanel
        {
            Name = "EditorActions", Dock = DockStyle.Bottom, Height = 56,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8), WrapContents = false,
        };
        cancel.Size = save.Size = new Size(150, 34);
        footer.Controls.Add(cancel); footer.Controls.Add(save);
        var split = new SplitContainer
        {
            Name = "EditorWorkspaceSplit", Dock = DockStyle.Fill, Size = new Size(1100, 650),
            FixedPanel = FixedPanel.Panel1, SplitterWidth = 8, SplitterDistance = 230,
            Panel1MinSize = 200, Panel2MinSize = 400,
        };
        navigation.Dock = content.Dock = DockStyle.Fill;
        split.Panel1.Controls.Add(navigation); split.Panel2.Controls.Add(content);
        foreach (var tree in navigation.Controls.OfType<TreeView>())
            tree.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        window.Controls.Add(split); window.Controls.Add(footer);
        footer.BringToFront(); split.BringToFront();
        window.ResumeLayout(true);
        EditorTheme.Apply(window);
    }
}
