using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Networking;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private void AddQuestShopEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems.Cast<ToolStripItem>()
            .Any(item => item.Name == "questShopEditorToolStripMenuItem"))
            return;

        var questShops = new ToolStripMenuItem
        {
            Name = "questShopEditorToolStripMenuItem",
            Text = "Quest Shops...",
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
        };

        questShops.Click += (_, _) =>
            PacketSender.SendRequestQuestShopConfiguration(openEditor: true);

        contentEditorsToolStripMenuItem.DropDownItems.Add(questShops);
    }

    public void OpenQuestShopEditor()
    {
        var editor = new FrmQuestShopConfiguration();
        editor.Show();
        editor.BringToFront();
    }
}
