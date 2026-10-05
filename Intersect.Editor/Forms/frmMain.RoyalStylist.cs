using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Networking;

namespace Intersect.Editor.Forms;

public partial class FrmMain
{
    private void AddRoyalStylistEditorMenu()
    {
        if (contentEditorsToolStripMenuItem.DropDownItems
            .Cast<ToolStripItem>()
            .Any(item =>
                item.Name == "royalStylistEditorToolStripMenuItem"))
        {
            return;
        }

        var stylist = new ToolStripMenuItem
        {
            Name = "royalStylistEditorToolStripMenuItem",
            Text = "Royal Stylists...",
            ForeColor =
                System.Drawing.Color.FromArgb(220, 220, 220),
        };

        stylist.Click += (_, _) =>
            PacketSender.SendRequestRoyalStylistConfiguration(
                openEditor: true
            );

        contentEditorsToolStripMenuItem.DropDownItems.Add(stylist);
    }

    public void OpenRoyalStylistEditor()
    {
        var editor = new FrmRoyalStylistConfiguration();
        editor.Show();
        editor.BringToFront();
    }
}
