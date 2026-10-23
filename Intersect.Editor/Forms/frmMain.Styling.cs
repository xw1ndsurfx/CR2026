using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;

namespace Intersect.Editor.Forms;
public partial class FrmMain
{
    private void ModernizeWorkspace()
    {
        Text = "Logiklik 2D Engine · " + Intersect.Framework.Core.BuildIdentity.Label;
        EditorTheme.Apply(this);
        foreach (var window in new Form[] { Globals.MapLayersWindow, Globals.MapListWindow, Globals.MapGridWindowNew, Globals.MapEditorWindow })
            EditorTheme.Apply(window);
        dockLeft.DockBackColor = EditorTheme.Background;
        dockLeft.BorderStyle = BorderStyle.None;
        menuStrip.AutoSize = false; menuStrip.Height = 32; menuStrip.Padding = new Padding(10, 3, 10, 3);
        toolStrip1.AutoSize = false; toolStrip1.Height = 38; toolStrip1.Padding = new Padding(10, 5, 10, 5);
        toolStrip1.ImageScalingSize = new Size(20, 20);
        foreach (ToolStripItem item in toolStrip1.Items)
            if (item is ToolStripButton) { item.AutoSize = false; item.Size = new Size(32, 28); item.Margin = new Padding(2, 0, 2, 0); }
        statusStrip1.Height = 30;
        var identity = new ToolStripLabel("LOGIKLIK 2D ENGINE") { Name = "LogiklikWorkspaceIdentity", Alignment = ToolStripItemAlignment.Right,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = EditorTheme.Accent, Margin = new Padding(16, 0, 6, 0) };
        menuStrip.Items.Add(identity);
        identity.ForeColor = EditorTheme.Accent;
        identity.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }
}
