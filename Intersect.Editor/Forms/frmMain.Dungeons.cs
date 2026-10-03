using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Networking;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private void AddDungeonEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems.Cast<ToolStripItem>()
            .Any(item => item.Name == "dungeonEditorToolStripMenuItem"))
            return;

        var dungeons = new ToolStripMenuItem
        {
            Name = "dungeonEditorToolStripMenuItem",
            Text = "Dungeons...",
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
        };

        dungeons.Click += (_, _) =>
            PacketSender.SendRequestDungeonConfiguration(openEditor: true);

        contentEditorsToolStripMenuItem.DropDownItems.Add(dungeons);
    }

    public void OpenDungeonEditor()
    {
        var editor = new FrmDungeonConfiguration();
        editor.Show();
        editor.BringToFront();
    }
}
